using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UsbHid.USB.Classes.DllWrappers;
using UsbHid.USB.Structures;

namespace UsbHid.USB.Classes
{
    public static class DeviceDiscovery
    {
        public static bool FindHidDevices(ref string[] listOfDevicePathNames, ref int numberOfDevicesFound)
        {
            Debug.WriteLine("usbGenericHidCommunication:findHidDevices() -> Method called");
            
            // Initialise the internal variables required for performing the search
            var bufferSize = 0;
            var detailDataBuffer = IntPtr.Zero;
            bool deviceFound;
            var deviceInfoSet = new IntPtr();
            var lastDevice = false;
            int listIndex;
            var deviceInterfaceData = new SpDeviceInterfaceData();

            // Get the required GUID
            var systemHidGuid = new Guid();
            Hid.HidD_GetHidGuid(ref systemHidGuid);
            Debug.WriteLine("usbGenericHidCommunication:findHidDevices() -> Fetched GUID for HID devices ({0})", systemHidGuid.ToString());

            try
            {
                // Here we populate a list of plugged-in devices matching our class GUID (DIGCF_PRESENT specifies that the list
                // should only contain devices which are plugged in)
                Debug.WriteLine("usbGenericHidCommunication:findHidDevices() -> Using SetupDiGetClassDevs to get all devices with the correct GUID");
                deviceInfoSet = SetupApi.SetupDiGetClassDevs(ref systemHidGuid, IntPtr.Zero, IntPtr.Zero, Constants.DigcfPresent | Constants.DigcfDeviceinterface);

                // Reset the deviceFound flag and the memberIndex counter
                deviceFound = false;
                listIndex = 0;

                deviceInterfaceData.cbSize = Marshal.SizeOf(deviceInterfaceData);

                // Look through the retrieved list of class GUIDs looking for a match on our interface GUID
                do
                {
                    //Debug.WriteLine("usbGenericHidCommunication:findHidDevices() -> Enumerating devices");
                    var success = SetupApi.SetupDiEnumDeviceInterfaces(deviceInfoSet,IntPtr.Zero,ref systemHidGuid, listIndex, ref deviceInterfaceData);

                    if (!success)
                    {
                        //Debug.WriteLine("usbGenericHidCommunication:findHidDevices() -> No more devices left - giving up");
                        lastDevice = true;
                    }
                    else
                    {
                        // The target device has been found, now we need to retrieve the device path so we can open
                        // the read and write handles required for USB communication

                        // First call is just to get the required buffer size for the real request
                        SetupApi.SetupDiGetDeviceInterfaceDetail
                            (deviceInfoSet,
                             ref deviceInterfaceData,
                             IntPtr.Zero,
                             0,
                             ref bufferSize,
                             IntPtr.Zero);

                        // Allocate some memory for the buffer
                        detailDataBuffer = Marshal.AllocHGlobal(bufferSize);
                        Marshal.WriteInt32(detailDataBuffer, (IntPtr.Size == 4) ? (4 + Marshal.SystemDefaultCharSize) : 8);

                        // Second call gets the detailed data buffer
                        //Debug.WriteLine("usbGenericHidCommunication:findHidDevices() -> Getting details of the device");
                        SetupApi.SetupDiGetDeviceInterfaceDetail
                            (deviceInfoSet,
                             ref deviceInterfaceData,
                             detailDataBuffer,
                             bufferSize,
                             ref bufferSize,
                             IntPtr.Zero);

                        // Skip over cbsize (4 bytes) to get the address of the devicePathName.
                        var pDevicePathName = new IntPtr(detailDataBuffer.ToInt32() + 4);

                        // Get the String containing the devicePathName.
                        listOfDevicePathNames[listIndex] = Marshal.PtrToStringAuto(pDevicePathName);

                        //Debug.WriteLine(string.Format("usbGenericHidCommunication:findHidDevices() -> Found matching device (memberIndex {0})", memberIndex));
                        deviceFound = true;
                    }
                    listIndex = listIndex + 1;
                }
                while (lastDevice != true);
            }
            catch (Exception)
            {
                // Something went badly wrong... output some debug and return false to indicated device discovery failure
                Debug.WriteLine("usbGenericHidCommunication:findHidDevices() -> EXCEPTION: Something went south whilst trying to get devices with matching GUIDs - giving up!");
                return false;
            }
            finally
            {
                // Clean up the unmanaged memory allocations
                if (detailDataBuffer != IntPtr.Zero)
                {
                    // Free the memory allocated previously by AllocHGlobal.
                    Marshal.FreeHGlobal(detailDataBuffer);
                }

                if (deviceInfoSet != IntPtr.Zero)
                {
                    SetupApi.SetupDiDestroyDeviceInfoList(deviceInfoSet);
                }
            }

            if (deviceFound)
            {
                Debug.WriteLine("usbGenericHidCommunication:findHidDevices() -> Found {0} devices with matching GUID", listIndex - 1);
                numberOfDevicesFound = listIndex - 2;
            }
            else Debug.WriteLine("usbGenericHidCommunication:findHidDevices() -> No matching devices found");

            return deviceFound;
        }

        public static bool FindTargetDevice(ref DeviceInformationStructure deviceInformation)
        {
            Debug.WriteLine("usbGenericHidCommunication:findTargetDevice() -> Method called");

            var listOfDevicePathNames = new String[128]; // 128 is the maximum number of USB devices allowed on a single host
            var numberOfDevicesFound = 0;

            try
            {
                // Reset the device detection flag
                var isDeviceDetected = false;
                deviceInformation.IsDeviceAttached = false;

                // Get all the devices with the correct HID GUID
                var deviceFoundByGuid = FindHidDevices(ref listOfDevicePathNames, ref numberOfDevicesFound);

                if (deviceFoundByGuid)
                {
                    Debug.WriteLine("usbGenericHidCommunication:findTargetDevice() -> Devices with matching GUID found...");
                    var listIndex = 0;

                    do
                    {
                        Debug.WriteLine("usbGenericHidCommunication:findTargetDevice() -> Performing CreateFile to listIndex {0}", listIndex);
                        deviceInformation.HidHandle = Kernel32.CreateFile(listOfDevicePathNames[listIndex], 0, Constants.FileShareRead | Constants.FileShareWrite, IntPtr.Zero, Constants.OpenExisting, 0, 0);

                        if (!deviceInformation.HidHandle.IsInvalid)
                        {
                            deviceInformation.Attributes.Size = Marshal.SizeOf(deviceInformation.Attributes);
                            var success = Hid.HidD_GetAttributes(deviceInformation.HidHandle, ref deviceInformation.Attributes);

                            if (success)
                            {
                                Debug.WriteLine("usbGenericHidCommunication:findTargetDevice() -> Found device with VID {0}, PID {1} and Version number {2}",
                                    Convert.ToString(deviceInformation.Attributes.VendorID, 16),
                                    Convert.ToString(deviceInformation.Attributes.ProductID, 16),
                                    Convert.ToString(deviceInformation.Attributes.VersionNumber, 16));

                                //  Do the VID and PID of the device match our target device?
                                if ((deviceInformation.Attributes.VendorID == deviceInformation.TargetVendorId) &&
                                    (deviceInformation.Attributes.ProductID == deviceInformation.TargetProductId))
                                {
                                    // Matching device found
                                    Debug.WriteLine("usbGenericHidCommunication:findTargetDevice() -> Device with matching VID and PID found!");
                                    isDeviceDetected = true;

                                    // Store the device's pathname in the device information
                                    deviceInformation.DevicePathName = listOfDevicePathNames[listIndex];
                                }
                                else
                                {
                                    // Wrong device, close the handle
                                    Debug.WriteLine("usbGenericHidCommunication:findTargetDevice() -> Device didn't match... Continuing...");
                                    deviceInformation.HidHandle.Close();
                                }
                            }
                            else
                            {
                                //  Something went rapidly south...  give up!
                                Debug.WriteLine("usbGenericHidCommunication:findTargetDevice() -> Something bad happened - couldn't fill the HIDD_ATTRIBUTES, giving up!");
                                deviceInformation.HidHandle.Close();
                            }
                        }

                        //  Move to the next device, or quit if there are no more devices to examine
                        listIndex++;
                    }
                    while (!((isDeviceDetected || (listIndex == numberOfDevicesFound + 1))));
                }

                // If we found a matching device then we need discover more details about the attached device
                // and then open read and write handles to the device to allow communication
                if (isDeviceDetected)
                {
                    // Query the HID device's capabilities (primarily we are only really interested in the 
                    // input and output report byte lengths as this allows us to validate information sent
                    // to and from the device does not exceed the devices capabilities.
                    //
                    // We could determine the 'type' of HID device here too, but since this class is only
                    // for generic HID communication we don't care...
                    QueryDeviceCapabilities(ref deviceInformation);

                    // Open the readHandle to the device
                    Debug.WriteLine("usbGenericHidCommunication:findTargetDevice() -> Opening a readHandle to the device");
                    deviceInformation.ReadHandle = Kernel32.CreateFile(
                        deviceInformation.DevicePathName,
                        Constants.GenericRead,
                        Constants.FileShareRead | Constants.FileShareWrite,
                        IntPtr.Zero, Constants.OpenExisting,
                        Constants.FileFlagOverlapped,
                        0);

                    // Did we open the readHandle successfully?
                    if (deviceInformation.ReadHandle.IsInvalid)
                    {
                        Debug.WriteLine("usbGenericHidCommunication:findTargetDevice() -> Unable to open a readHandle to the device!");
                        return false;
                    }
                
                    Debug.WriteLine("usbGenericHidCommunication:findTargetDevice() -> Opening a writeHandle to the device");
                    deviceInformation.WriteHandle = Kernel32.CreateFile(
                        deviceInformation.DevicePathName,
                        Constants.GenericWrite,
                        Constants.FileShareRead | Constants.FileShareWrite,
                        IntPtr.Zero,
                        Constants.OpenExisting, 0, 0);

                    // Did we open the writeHandle successfully?
                    if (deviceInformation.WriteHandle.IsInvalid)
                    {
                        Debug.WriteLine("usbGenericHidCommunication:findTargetDevice() -> Unable to open a writeHandle to the device!");

                        // Attempt to close the writeHandle
                        deviceInformation.WriteHandle.Close();
                        return false;
                    }
                    
                    // Device is now discovered and ready for use, update the status
                    deviceInformation.IsDeviceAttached = true;
                    return true;
                }
                
                //  The device wasn't detected.
                Debug.WriteLine("usbGenericHidCommunication:findTargetDevice() -> No matching device found!");
                return false;
            }
            catch (Exception)
            {
                Debug.WriteLine("usbGenericHidCommunication:findTargetDevice() -> EXCEPTION: Unknown - device not found");
                return false;
            }
        }

        public static void QueryDeviceCapabilities(ref DeviceInformationStructure deviceInformation)
        {
            var preparsedData = new IntPtr();

            try
            {
                // Get the preparsed data from the HID driver
                Hid.HidD_GetPreparsedData(deviceInformation.HidHandle, ref preparsedData);

                // Get the HID device's capabilities
                var result = Hid.HidP_GetCaps(preparsedData, ref deviceInformation.Capabilities);
                if ((result == 0)) return;
            
                Debug.WriteLine("usbGenericHidCommunication:queryDeviceCapabilities() -> Device query results:");
                Debug.WriteLine("usbGenericHidCommunication:queryDeviceCapabilities() ->     Usage: {0}",
                                              Convert.ToString(deviceInformation.Capabilities.Usage, 16));
                Debug.WriteLine("usbGenericHidCommunication:queryDeviceCapabilities() ->     Usage Page: {0}",
                                              Convert.ToString(deviceInformation.Capabilities.UsagePage, 16));
                Debug.WriteLine("usbGenericHidCommunication:queryDeviceCapabilities() ->     Input Report Byte Length: {0}",
                                              deviceInformation.Capabilities.InputReportByteLength);
                Debug.WriteLine("usbGenericHidCommunication:queryDeviceCapabilities() ->     Output Report Byte Length: {0}",
                                              deviceInformation.Capabilities.OutputReportByteLength);
                Debug.WriteLine("usbGenericHidCommunication:queryDeviceCapabilities() ->     Feature Report Byte Length: {0}",
                                              deviceInformation.Capabilities.FeatureReportByteLength);
                Debug.WriteLine("usbGenericHidCommunication:queryDeviceCapabilities() ->     Number of Link Collection Nodes: {0}",
                                              deviceInformation.Capabilities.NumberLinkCollectionNodes);
                Debug.WriteLine("usbGenericHidCommunication:queryDeviceCapabilities() ->     Number of Input Button Caps: {0}",
                                              deviceInformation.Capabilities.NumberInputButtonCaps);
                Debug.WriteLine("usbGenericHidCommunication:queryDeviceCapabilities() ->     Number of Input Value Caps: {0}",
                                              deviceInformation.Capabilities.NumberInputValueCaps);
                Debug.WriteLine("usbGenericHidCommunication:queryDeviceCapabilities() ->     Number of Input Data Indices: {0}",
                                              deviceInformation.Capabilities.NumberInputDataIndices);
                Debug.WriteLine("usbGenericHidCommunication:queryDeviceCapabilities() ->     Number of Output Button Caps: {0}",
                                              deviceInformation.Capabilities.NumberOutputButtonCaps);
                Debug.WriteLine("usbGenericHidCommunication:queryDeviceCapabilities() ->     Number of Output Value Caps: {0}",
                                              deviceInformation.Capabilities.NumberOutputValueCaps);
                Debug.WriteLine("usbGenericHidCommunication:queryDeviceCapabilities() ->     Number of Output Data Indices: {0}",
                                              deviceInformation.Capabilities.NumberOutputDataIndices);
                Debug.WriteLine("usbGenericHidCommunication:queryDeviceCapabilities() ->     Number of Feature Button Caps: {0}",
                                              deviceInformation.Capabilities.NumberFeatureButtonCaps);
                Debug.WriteLine("usbGenericHidCommunication:queryDeviceCapabilities() ->     Number of Feature Value Caps: {0}",
                                              deviceInformation.Capabilities.NumberFeatureValueCaps);
                Debug.WriteLine("usbGenericHidCommunication:queryDeviceCapabilities() ->     Number of Feature Data Indices: {0}",
                                              deviceInformation.Capabilities.NumberFeatureDataIndices);
            }
            catch (Exception)
            {
                // Something went badly wrong... this shouldn't happen, so we throw an exception
                Debug.WriteLine("usbGenericHidCommunication:queryDeviceCapabilities() -> EXECEPTION: An unrecoverable error has occurred!");
                throw;
            }
            finally
            {
                // Free up the memory before finishing
                if (preparsedData != IntPtr.Zero)
                {
                    Hid.HidD_FreePreparsedData(preparsedData);
                }
            }
        }      
    }
}
