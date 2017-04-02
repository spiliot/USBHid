using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using UsbHid.USB.Classes.DllWrappers;
using UsbHid.USB.Structures;

namespace UsbHid.USB.Classes
{
    public class DeviceChangeNotifier : Form
    {
        public delegate void DeviceNotifyDelegate(Message msg);
        public static event DeviceNotifyDelegate DeviceNotify;

        public delegate void DeviceAttachedDelegate();
        public static event DeviceAttachedDelegate DeviceAttached;

        public delegate void DeviceDetachedDelegate();
        public static event DeviceDetachedDelegate DeviceDetached;

        public IntPtr DeviceNotificationHandle;

        private static string devicepath;

        public DeviceChangeNotifier()
        {
            RegisterForDeviceNotifications(Handle);
        }

        private static DeviceChangeNotifier mInstance;

        public static void Start( string devicePath )
        {
            devicepath = devicePath;
            var t = new Thread(RunForm);
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
        }
        
        public static void Stop()
        {
            try
            {
                if (mInstance == null) throw new InvalidOperationException("Notifier not started");
                DeviceNotify = null;
                mInstance.Invoke(new MethodInvoker(mInstance.EndForm));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private static void RunForm()
        {
            Application.Run(new DeviceChangeNotifier());
        }

        private void EndForm()
        {
            Close();
        }

        protected override void SetVisibleCore(bool value)
        {
            // Prevent window getting visible
            if (mInstance == null) 
            {
                mInstance = this;
                try
                {
                    CreateHandle();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }

            base.SetVisibleCore(false);
        }
        
        protected override void WndProc(ref Message m)
        {
            // Trap WM_DEVICECHANGE
            if (m.Msg == 0x219)
            {
                if (!IsNotificationForTargetDevice(m)) return;
                HandleDeviceNotificationMessages(m);
            }
            base.WndProc(ref m);
        }

        /// <summary>
        /// registerForDeviceNotification - registers the window (identified by the windowHandle) for 
        /// device notification messages from Windows
        /// </summary>
        public bool RegisterForDeviceNotifications(IntPtr windowHandle1)
        {
            Debug.WriteLine("usbGenericHidCommunication:registerForDeviceNotifications() -> Method called");

            // A DEV_BROADCAST_DEVICEINTERFACE header holds information about the request.
            var devBroadcastDeviceInterface = new DevBroadcastDeviceinterface();
            var devBroadcastDeviceInterfaceBuffer = IntPtr.Zero;

            // Get the required GUID
            var systemHidGuid = new Guid();
            Hid.HidD_GetHidGuid(ref systemHidGuid);

            try
            {
                // Set the parameters in the DEV_BROADCAST_DEVICEINTERFACE structure.
                var size = Marshal.SizeOf(devBroadcastDeviceInterface);
                devBroadcastDeviceInterface.dbcc_size = size;
                devBroadcastDeviceInterface.dbcc_devicetype = Constants.DbtDevtypDeviceinterface;
                devBroadcastDeviceInterface.dbcc_reserved = 0;
                devBroadcastDeviceInterface.dbcc_classguid = systemHidGuid;

                devBroadcastDeviceInterfaceBuffer = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(devBroadcastDeviceInterface, devBroadcastDeviceInterfaceBuffer, true);
                // Register for notifications and store the returned handle
                DeviceNotificationHandle = User32.RegisterDeviceNotification(Handle, devBroadcastDeviceInterfaceBuffer, Constants.DeviceNotifyWindowHandle);

                Marshal.PtrToStructure(devBroadcastDeviceInterfaceBuffer, devBroadcastDeviceInterface);

                if ((DeviceNotificationHandle.ToInt32() == IntPtr.Zero.ToInt32()))
                {
                    Debug.WriteLine(
                        "usbGenericHidCommunication:registerForDeviceNotifications() -> Notification registration failed");
                    return false;
                }
                else
                {
                    Debug.WriteLine(
                        "usbGenericHidCommunication:registerForDeviceNotifications() -> Notification registration succeded");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "usbGenericHidCommunication:registerForDeviceNotifications() -> EXCEPTION: An unknown exception has occured!");
                Debug.WriteLine(ex.Message);
            }
            finally
            {
                // Free the memory allocated previously by AllocHGlobal.
                if (devBroadcastDeviceInterfaceBuffer != IntPtr.Zero)
                {
                    try
                    {
                        Marshal.FreeHGlobal(devBroadcastDeviceInterfaceBuffer);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                }
            } 

            return false;
        }

        public static bool IsNotificationForTargetDevice(Message m)
        {
            if (string.IsNullOrEmpty(devicepath)) return false;

            try
            {
                var devBroadcastDeviceInterface = new DevBroadcastDeviceinterface1();
                var devBroadcastHeader = new DevBroadcastHdr();

                try
                {
                    Marshal.PtrToStructure(m.LParam, devBroadcastHeader);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    return false;
                }


                // Is the notification event concerning a device interface?
                if ((devBroadcastHeader.dbch_devicetype == Constants.DbtDevtypDeviceinterface))
                {
                    // Get the device path name of the affected device
                    var stringSize = Convert.ToInt32((devBroadcastHeader.dbch_size - 32) / 2);
                    devBroadcastDeviceInterface.dbcc_name = new Char[stringSize + 1];
                    Marshal.PtrToStructure(m.LParam, devBroadcastDeviceInterface);
                    var deviceNameString = new string(devBroadcastDeviceInterface.dbcc_name, 0, stringSize);
                    // Compare the device name with our target device's pathname (strings are moved to lower case
                    return (string.Compare(deviceNameString.ToLower(), devicepath.ToLower(), StringComparison.OrdinalIgnoreCase) == 0);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("usbGenericHidCommunication:isNotificationForTargetDevice() -> EXCEPTION: An unknown exception has occured!");
                Debug.WriteLine(ex.Message);
                return false;
            }
            return false;
        }

        public void HandleDeviceNotificationMessages(Message m)
        {
            //Debug.WriteLine("usbGenericHidCommunication:handleDeviceNotificationMessages() -> Method called");

            // Make sure this is a device notification
            if (m.Msg != Constants.WmDevicechange) return;

            Debug.WriteLine("usbGenericHidCommunication:handleDeviceNotificationMessages() -> Device notification received");

            try
            {
                switch (m.WParam.ToInt32())
                {
                    // Device attached
                    case Constants.DbtDevicearrival:
                        Debug.WriteLine("usbGenericHidCommunication:handleDeviceNotificationMessages() -> New device attached");
                        // If our target device is not currently attached, this could be our device, so we attempt to find it.
                        ReportDeviceAttached(m);
                        break;

                    // Device removed
                    case Constants.DbtDeviceremovecomplete:
                        Debug.WriteLine("usbGenericHidCommunication:handleDeviceNotificationMessages() -> A device has been removed");

                        // Was this our target device?  
                        if (IsNotificationForTargetDevice(m))
                        {
                            // If so detach the USB device.
                            Debug.WriteLine("usbGenericHidCommunication:handleDeviceNotificationMessages() -> The target USB device has been removed - detaching...");
                            ReportDeviceDetached(m);
                        }
                        break;

                    // Other message
                    default:
                        Debug.WriteLine("usbGenericHidCommunication:handleDeviceNotificationMessages() -> Unknown notification message");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("usbGenericHidCommunication:handleDeviceNotificationMessages() -> EXCEPTION: An unknown exception has occured!");
                Debug.WriteLine(ex.Message);
            }
        }

        private void ReportDeviceDetached(Message message)
        {
            if (DeviceDetached != null) DeviceDetached();
            if (DeviceNotify != null) DeviceNotify(message);
        }

        private void ReportDeviceAttached(Message message)
        {
            if (DeviceAttached != null) DeviceAttached();
            if (DeviceNotify != null) DeviceNotify(message);
        }
    }
}
