using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UsbHid.USB.Classes.DllWrappers;
using UsbHid.USB.Structures;

namespace UsbHid.USB.Classes
{
    public static class DeviceDiscovery
    {
        public static List<string> FindAllHidDevices()
        {
            var listOfDevicePathNames = new List<string>();
            var detailDataBuffer = IntPtr.Zero;
            var deviceInfoSet = new IntPtr();
            int listIndex = 0;
            var deviceInterfaceData = new SpDeviceInterfaceData();

            int lasterror = 0;

            // Get the required HID class GUID
            var systemHidGuid = new Guid();
            Hid.HidD_GetHidGuid(ref systemHidGuid);

            deviceInfoSet = SetupDiGetClassDevs(ref systemHidGuid);
            deviceInterfaceData.cbSize = Marshal.SizeOf(deviceInterfaceData);

            try
            {
                // Look through the retrieved list of class GUIDs looking for a match on our interface GUID
                // SetupDiEnumDeviceInterfaces will return false if it fails for any reason, including when no more items are left
                // so we need to keep looping until the last thrown error is ERROR_NO_MORE_ITEMS
                // Note: we post increment lastIndex so each subsequent call refers to a new device
                while (SetupApi.SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref systemHidGuid, listIndex++, ref deviceInterfaceData) || (lasterror = Marshal.GetLastWin32Error()) != Constants.ERROR_NO_MORE_ITEMS)
                {
                    if (lasterror != 0)
                    {
                        // SetupDiEnumDeviceInterfaces failed and it wasn't ERROR_NO_MORE_ITEMS as this would have stopped the loop
                        Debug.WriteLine("SetupDiEnumDeviceInterfaces failed for run {0} with error {1}", listIndex, lasterror);
                        continue;
                    }

                    int bufferSize = 0;

                    // The target device has been found, now we need to retrieve the device path so we can open
                    // the read and write handles required for USB communication

                    // First call fails with ERROR_INSUFFICIENT_BUFFER and is used just to get the required buffer size for the real request
                    var success = SetupApi.SetupDiGetDeviceInterfaceDetail(
                        deviceInfoSet,
                        ref deviceInterfaceData,
                        IntPtr.Zero,
                        0,
                        ref bufferSize,
                        IntPtr.Zero
                    );

                    // Allocate some memory for the buffer
                    detailDataBuffer = Marshal.AllocHGlobal(bufferSize);
                    Marshal.WriteInt32(detailDataBuffer, (IntPtr.Size == 4) ? (4 + Marshal.SystemDefaultCharSize) : 8);

                    // Second call gets the detailed data buffer
                    success = SetupApi.SetupDiGetDeviceInterfaceDetail(
                        deviceInfoSet,
                        ref deviceInterfaceData,
                        detailDataBuffer,
                        bufferSize,
                        ref bufferSize,
                        IntPtr.Zero
                    );

                    if (!success)
                    {
                        Debug.WriteLine("SetupDiGetDeviceInterfaceDetail failed for run {0} with error {1}", listIndex, Marshal.GetLastWin32Error());
                        continue;
                    }


                    // Skip over cbsize (4 bytes) to get the address of the devicePathName.
                    var pDevicePathName = IntPtr.Add(detailDataBuffer, 4);

                    // Get the String containing the devicePathName.
                    listOfDevicePathNames.Add(Marshal.PtrToStringAuto(pDevicePathName));
                }
            }
            catch (Exception)
            {
                // Something went badly wrong...
                listOfDevicePathNames.Clear();
            }
            finally
            {
                // Clean up the unmanaged memory allocations and free resources held by the windows API
                Marshal.FreeHGlobal(detailDataBuffer);
                SetupApi.SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }

            return listOfDevicePathNames;
        }

        private static IntPtr SetupDiGetClassDevs(ref Guid guid)
        {
            // Here we populate a list of plugged-in devices matching our class GUID (DIGCF_PRESENT specifies that the list
            // should only contain devices which are plugged in)

            var returnedPointer = IntPtr.Zero;
            try
            {
                returnedPointer = SetupApi.SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, Constants.DigcfPresent | Constants.DigcfDeviceinterface);
            }
            catch
            {
                Debug.WriteLine("SetupDiGetClassDevs failed with error code {0}", Marshal.GetLastWin32Error());
            }
            return returnedPointer;
        }

        public static List<KeyValuePair<string, UsbDescriptorStrings>> FindHidDevices(IUsbDeviceMatchable matchingRules)
        {
            var matchingDeviceInstancePaths = new List<KeyValuePair<string, UsbDescriptorStrings>>();

            foreach (string deviceInstancePath in FindAllHidDevices())
            {
                if (!matchingRules.BasicMatch(deviceInstancePath))
                {
                    continue;
                }

                var deviceHandle = GetDeviceInformationHidHandle(deviceInstancePath);
                UsbDescriptorStrings descriptorStrings;

                if (deviceHandle.IsInvalid || !(matchingRules.DescriptorsMatch(descriptorStrings = GetDescriptionStrings(deviceHandle))))
                {
                    deviceHandle.Close();
                    continue;
                }

                matchingDeviceInstancePaths.Add(new KeyValuePair<string, UsbDescriptorStrings>(deviceInstancePath, descriptorStrings));
            }

            return matchingDeviceInstancePaths;
        }

        private static UsbDescriptorStrings GetDescriptionStrings(SafeFileHandle deviceHadle)
        {
            const int stringSizelimit = 64; //TODO: While this should cover almost all cases it is hardcoded which is bad
            UsbDescriptorStrings descriptorStrings;

            var manufacturer = new StringBuilder(stringSizelimit);
            var product = new StringBuilder(stringSizelimit);
            var serial = new StringBuilder(stringSizelimit);

            try
            {
                Hid.HidD_GetManufacturerString(deviceHadle, manufacturer, stringSizelimit);
                Hid.HidD_GetProductString(deviceHadle, product, stringSizelimit);
                Hid.HidD_GetSerialNumberString(deviceHadle, serial, stringSizelimit);
            }
            finally
            {
                descriptorStrings = new UsbDescriptorStrings(manufacturer.ToString(), product.ToString(), serial.ToString());
            }
            return descriptorStrings;
        }

        private static bool GetAttributes(ref DeviceInformationStructure deviceInformation)
        {
            try
            {
                deviceInformation.Attributes.Size = Marshal.SizeOf(deviceInformation.Attributes);

                if (Hid.HidD_GetAttributes(deviceInformation.HidHandle, ref deviceInformation.Attributes))
                {
                    return true;
                }
            }
            catch
            {
                Debug.WriteLine("GetAttributes failed");
                return false;
            }
            return false;
        }

        public static bool FindTargetDevice(ref DeviceInformationStructure deviceInformation)
        {
            deviceInformation.HidHandle = GetDeviceInformationHidHandle(deviceInformation.DevicePathName);
            deviceInformation.ReadHandle = GetDeviceInformationReadHandle(deviceInformation.DevicePathName);
            deviceInformation.WriteHandle = GetDeviceInformationWriteHandle(deviceInformation.DevicePathName);

            if (deviceInformation.HidHandle.IsInvalid || deviceInformation.ReadHandle.IsInvalid || deviceInformation.WriteHandle.IsInvalid)
            {
                return false;
            }

            if (!GetAttributes(ref deviceInformation)) return false;

            deviceInformation.DescriptorStrings = GetDescriptionStrings(deviceInformation.HidHandle);

            if (!QueryDeviceCapabilities(ref deviceInformation)) return false;

            deviceInformation.IsDeviceAttached = true;

            return true;
        }

        private static SafeFileHandle GetDeviceInformationHidHandle(string deviceInstancePath)
        {
            SafeFileHandle deviceHandle = null;
            try
            {
                deviceHandle = Kernel32.CreateFile(deviceInstancePath, 0, Constants.FileShareRead | Constants.FileShareWrite, IntPtr.Zero, Constants.OpenExisting, 0, 0);
            }
            catch
            {
                Debug.WriteLine("GetDeviceInformationHidHandle failed for {0}", deviceInstancePath);
            }

            return deviceHandle;
        }

        private static SafeFileHandle GetDeviceInformationReadHandle(string deviceInstancePath)
        {
            SafeFileHandle deviceHandle = null;
            try
            {
                deviceHandle = Kernel32.CreateFile(deviceInstancePath, Constants.GenericRead, Constants.FileShareRead | Constants.FileShareWrite, IntPtr.Zero, Constants.OpenExisting, Constants.FileFlagOverlapped, 0);
            }
            catch
            {
                Debug.WriteLine("GetDeviceInformationReadHandle failed for {0}", deviceInstancePath);
            }

            return deviceHandle;
        }

        private static SafeFileHandle GetDeviceInformationWriteHandle(string deviceInstancePath)
        {
            SafeFileHandle deviceHandle = null;
            try
            {
                deviceHandle = Kernel32.CreateFile(deviceInstancePath, Constants.GenericWrite, Constants.FileShareRead | Constants.FileShareWrite, IntPtr.Zero, Constants.OpenExisting, 0, 0);
            }
            catch
            {
                Debug.WriteLine("GetDeviceInformationWriteHandle failed for {0}", deviceInstancePath);
            }

            return deviceHandle;
        }

        public static bool QueryDeviceCapabilities(ref DeviceInformationStructure deviceInformation)
        {
            var preparsedData = new IntPtr();

            try
            {
                Hid.HidD_GetPreparsedData(deviceInformation.HidHandle, ref preparsedData);
                Hid.HidP_GetCaps(preparsedData, ref deviceInformation.Capabilities);
            }
            catch
            {
                Debug.WriteLine("QueryDeviceCapabilities failed with error {0}", Marshal.GetLastWin32Error());
                return false;
            }
            finally
            {
                // Free up the memory before finishing
                Hid.HidD_FreePreparsedData(preparsedData);
            }
            return true;
        }      
    }
}
