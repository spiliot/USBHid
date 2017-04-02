using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UsbHid.USB.Classes.DllWrappers;
using UsbHid.USB.Structures;

namespace UsbHid.USB.Classes
{
    public static class DeviceDiscovery
    {
        public static List<string> FindHidDevices()
        {
            var listOfDevicePathNames = new List<string>();
            var bufferSize = 0;
            var detailDataBuffer = IntPtr.Zero;
            var deviceInfoSet = new IntPtr();
            int listIndex = 0;
            var deviceInterfaceData = new SpDeviceInterfaceData();

            // Get the required HID class GUID
            var systemHidGuid = new Guid();
            Hid.HidD_GetHidGuid(ref systemHidGuid);

            try
            {
                // Here we populate a list of plugged-in devices matching our class GUID (DIGCF_PRESENT specifies that the list
                // should only contain devices which are plugged in)
                deviceInfoSet = SetupApi.SetupDiGetClassDevs(ref systemHidGuid, IntPtr.Zero, IntPtr.Zero, Constants.DigcfPresent | Constants.DigcfDeviceinterface);
                deviceInterfaceData.cbSize = Marshal.SizeOf(deviceInterfaceData);

                // Look through the retrieved list of class GUIDs looking for a match on our interface GUID
                while (SetupApi.SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref systemHidGuid, listIndex, ref deviceInterfaceData))
                {
                    // The target device has been found, now we need to retrieve the device path so we can open
                    // the read and write handles required for USB communication

                    // First call is just to get the required buffer size for the real request
                    SetupApi.SetupDiGetDeviceInterfaceDetail(
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
                    SetupApi.SetupDiGetDeviceInterfaceDetail(
                        deviceInfoSet,
                        ref deviceInterfaceData,
                        detailDataBuffer,
                        bufferSize,
                        ref bufferSize,
                        IntPtr.Zero
                    );

                    // Skip over cbsize (4 bytes) to get the address of the devicePathName.
                    var pDevicePathName = new IntPtr(detailDataBuffer.ToInt32() + 4);

                    // Get the String containing the devicePathName.
                    listOfDevicePathNames.Add(Marshal.PtrToStringAuto(pDevicePathName));

                    listIndex++;
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

        public static bool FindTargetDevice(ref DeviceInformationStructure deviceInformation)
        {
            deviceInformation.IsDeviceAttached = false;

            // Get all the devices with the correct HID GUID
            List<string> devicesFound = FindHidDevices();

            if (devicesFound.Count == 0) return false;

            foreach (string devicepath in devicesFound)
            {
                try
                {
                    deviceInformation.HidHandle = Kernel32.CreateFile(devicepath, 0, Constants.FileShareRead | Constants.FileShareWrite, IntPtr.Zero, Constants.OpenExisting, 0, 0);

                    if (deviceInformation.HidHandle.IsInvalid) continue;

                    deviceInformation.Attributes.Size = Marshal.SizeOf(deviceInformation.Attributes);

                    if (!Hid.HidD_GetAttributes(deviceInformation.HidHandle, ref deviceInformation.Attributes))
                    {
                        deviceInformation.HidHandle.Close();
                        continue;
                    }

                    //  Do the VID and PID of the device match our target device?
                    if ((deviceInformation.Attributes.VendorID != deviceInformation.TargetVendorId) ||
                        (deviceInformation.Attributes.ProductID != deviceInformation.TargetProductId))
                    {
                        // Wrong device, close the handle
                        deviceInformation.HidHandle.Close();
                        continue;
                    }

                    // Matching device found

                    // Store the device's pathname in the device information
                    deviceInformation.DevicePathName = devicepath;

                    // We found a matching device then we need discover more details about the attached device
                    // and then open read and write handles to the device to allow communication

                    // Query the HID device's capabilities (primarily we are only really interested in the 
                    // input and output report byte lengths as this allows us to validate information sent
                    // to and from the device does not exceed the devices capabilities.
                    QueryDeviceCapabilities(ref deviceInformation);

                    deviceInformation.ReadHandle = Kernel32.CreateFile(
                        deviceInformation.DevicePathName,
                        Constants.GenericRead,
                        Constants.FileShareRead | Constants.FileShareWrite,
                        IntPtr.Zero, Constants.OpenExisting,
                        Constants.FileFlagOverlapped,
                        0
                    );

                    deviceInformation.WriteHandle = Kernel32.CreateFile(
                        deviceInformation.DevicePathName,
                        Constants.GenericWrite,
                        Constants.FileShareRead | Constants.FileShareWrite,
                        IntPtr.Zero,
                        Constants.OpenExisting,
                        0,
                        0
                    );

                    if (deviceInformation.ReadHandle.IsInvalid || deviceInformation.WriteHandle.IsInvalid)
                    {
                        deviceInformation.ReadHandle.Close();
                        deviceInformation.WriteHandle.Close();
                        return false;
                    }

                    // Device is now discovered and ready for use, update the status
                    deviceInformation.IsDeviceAttached = true;
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return false;
        }

        public static void QueryDeviceCapabilities(ref DeviceInformationStructure deviceInformation)
        {
            var preparsedData = new IntPtr();

            try
            {
                Hid.HidD_GetPreparsedData(deviceInformation.HidHandle, ref preparsedData);
                Hid.HidP_GetCaps(preparsedData, ref deviceInformation.Capabilities);
            }
            catch (Exception)
            {
                // Something went badly wrong... this shouldn't happen, so we throw an exception
                throw;
            }
            finally
            {
                // Free up the memory before finishing
                Hid.HidD_FreePreparsedData(preparsedData);
            }
        }      
    }
}
