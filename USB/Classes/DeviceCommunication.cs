using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using UsbHid.USB.Classes.DllWrappers;
using UsbHid.USB.Structures;

namespace UsbHid.USB.Classes
{
    public static class DeviceCommunication
    {
        public static bool WriteRawReportToDevice(byte[] outputReportBuffer, ref DeviceInformationStructure deviceInformation)
        {
            // Make sure a device is attached
            if (!deviceInformation.IsDeviceAttached)
            {
                return false;
            }

            var numberOfBytesWritten = 0;

            try
            {
                // Set an output report via interrupt to the device
                var success = Kernel32.WriteFile(
                    deviceInformation.WriteHandle,
                    outputReportBuffer,
                    outputReportBuffer.Length,
                    ref numberOfBytesWritten,
                    IntPtr.Zero);

                return success;
            }
            catch (Exception)
            {
                // An error - send out some debug and return failure
                return false;
            }
        }

        public static bool ReadRawReportFromDeviceAsync(ref byte[] inputReportBuffer, ref int numberOfBytesRead, ref DeviceInformationStructure deviceInformation)
        {
            bool success;
            // Make sure a device is attached
            if (!deviceInformation.IsDeviceAttached)
            {
                return false;
            }

            IntPtr eventObject;
            var hidOverlapped = new NativeOverlapped();
            IntPtr nonManagedBuffer;
            IntPtr nonManagedOverlapped;

            try
            {
                // Prepare an event object for the overlapped ReadFile
                eventObject = Kernel32.CreateEvent(IntPtr.Zero, false, false, "");

                hidOverlapped.OffsetLow = 0;
                hidOverlapped.OffsetHigh = 0;
                hidOverlapped.EventHandle = eventObject;

                // Allocate memory for the unmanaged input buffer and overlap structure.
                nonManagedBuffer = Marshal.AllocHGlobal(inputReportBuffer.Length);
                nonManagedOverlapped = Marshal.AllocHGlobal(Marshal.SizeOf(hidOverlapped));
                Marshal.StructureToPtr(hidOverlapped, nonManagedOverlapped, false);

                // Read the input report buffer
                success = Kernel32.ReadFile(
                    deviceInformation.ReadHandle,
                    nonManagedBuffer,
                    inputReportBuffer.Length,
                    ref numberOfBytesRead,
                    nonManagedOverlapped);

                // Report receieved correctly, copy the unmanaged input buffer over to the managed buffer
                Marshal.Copy(nonManagedBuffer, inputReportBuffer, 0, numberOfBytesRead);
           }
            catch (Exception)
            {
                return false;
            }

            // Release non-managed objects before returning
            Marshal.FreeHGlobal(nonManagedBuffer);
            Marshal.FreeHGlobal(nonManagedOverlapped);

            // Close the file handle to release the object
            Kernel32.CloseHandle(eventObject);

            return success;
        }

        public static bool ReadRawReportFromDevice(ref byte[] inputReportBuffer, ref int numberOfBytesRead, ref DeviceInformationStructure deviceInformation)
        {
            // Make sure a device is attached
            if (!deviceInformation.IsDeviceAttached)
            {
                return false;
            }

            IntPtr eventObject;
            var hidOverlapped = new NativeOverlapped();
            IntPtr nonManagedBuffer;
            IntPtr nonManagedOverlapped;

            try
            {
                // Prepare an event object for the overlapped ReadFile
                eventObject = Kernel32.CreateEvent(IntPtr.Zero, false, false, "");

                hidOverlapped.OffsetLow = 0;
                hidOverlapped.OffsetHigh = 0;
                hidOverlapped.EventHandle = eventObject;

                // Allocate memory for the unmanaged input buffer and overlap structure.
                nonManagedBuffer = Marshal.AllocHGlobal(inputReportBuffer.Length);
                nonManagedOverlapped = Marshal.AllocHGlobal(Marshal.SizeOf(hidOverlapped));
                Marshal.StructureToPtr(hidOverlapped, nonManagedOverlapped, false);

                // Read the input report buffer
                var success = Kernel32.ReadFile(
                    deviceInformation.ReadHandle,
                    nonManagedBuffer,
                    inputReportBuffer.Length,
                    ref numberOfBytesRead,
                    nonManagedOverlapped);

                if (!success)
                {
                    // We are now waiting for the FileRead to complete
                    // Wait a maximum of 3 seconds for the FileRead to complete
                    var result = Kernel32.WaitForSingleObject(eventObject, 3000);

                    switch (result)
                    {
                            // Has the ReadFile completed successfully?
                        case Constants.WaitObject0:

                            // Get the number of bytes transferred
                            Kernel32.GetOverlappedResult(deviceInformation.ReadHandle, nonManagedOverlapped, ref numberOfBytesRead, false);
                            break;

                            // Did the FileRead operation timeout?
                        case Constants.WaitTimeout:

                            // Cancel the ReadFile operation
                            Kernel32.CancelIo(deviceInformation.ReadHandle);
                            if (!deviceInformation.HidHandle.IsInvalid) deviceInformation.HidHandle.Close();
                            if (!deviceInformation.ReadHandle.IsInvalid) deviceInformation.ReadHandle.Close();
                            if (!deviceInformation.WriteHandle.IsInvalid) deviceInformation.WriteHandle.Close();

                            // Detach the USB device to try to get us back in a known state
                            //detachUsbDevice();

                            return false;
                        
                        // Some other unspecified error has occurred?
                        default:

                            // Cancel the ReadFile operation

                            // Detach the USB device to try to get us back in a known state
                            
                            return false;
                    }
                }
                // Report receieved correctly, copy the unmanaged input buffer over to the managed buffer
                Marshal.Copy(nonManagedBuffer, inputReportBuffer, 0, numberOfBytesRead);
            }
            catch (Exception)
            {
                // An error - send out some debug and return failure
                return false;
            }

            // Release non-managed objects before returning
            Marshal.FreeHGlobal(nonManagedBuffer);
            Marshal.FreeHGlobal(nonManagedOverlapped);

            // Close the file handle to release the object
            Kernel32.CloseHandle(eventObject);

            return true;
        }

        public static bool ReadSingleReportFromDevice(ref byte[] inputReportBuffer, ref DeviceInformationStructure deviceInformation)
        {
            var numberOfBytesRead = 0;

            // The size of our inputReportBuffer must be at least the same size as the input report.
            if (inputReportBuffer.Length != deviceInformation.Capabilities.InputReportByteLength)
            {
                // inputReportBuffer is not the right length!
                return false;
            }

            // The readRawReportFromDevice method will fill the passed readBuffer or return false
            return ReadRawReportFromDevice(ref inputReportBuffer, ref numberOfBytesRead, ref deviceInformation);
        }

        public static bool ReadMultipleReportsFromDevice(ref byte[] inputReportBuffer, int numberOfReports, ref DeviceInformationStructure deviceInformation)
        {
            var success = false;
            var numberOfBytesRead = 0;
            long pointerToBuffer = 0;

            // Define a temporary buffer for assembling partial data reads into the completed inputReportBuffer
            var temporaryBuffer = new Byte[inputReportBuffer.Length];

            // Range check the number of reports
            if (numberOfReports == 0)
            {
                return false;
            }

            if (numberOfReports > 128)
            {
                return false;
            }

            // The size of our inputReportBuffer must be at least the same size as the input report multiplied by the number of reports requested.
            if (inputReportBuffer.Length != (deviceInformation.Capabilities.InputReportByteLength * numberOfReports))
            {
                // inputReportBuffer is not the right length!
                return false;
            }

            // The readRawReportFromDevice method will fill the passed read buffer or return false
            while (pointerToBuffer != (deviceInformation.Capabilities.InputReportByteLength * numberOfReports))
            {
                success = ReadRawReportFromDevice(ref temporaryBuffer, ref numberOfBytesRead, ref deviceInformation);

                // Was the read successful?
                if (!success)
                {
                    return false;
                }

                // Copy the received data into the referenced input buffer
                Array.Copy(temporaryBuffer, 0, inputReportBuffer, pointerToBuffer, numberOfBytesRead);
                pointerToBuffer += numberOfBytesRead;
            }

            return success;
        }
    }
}
