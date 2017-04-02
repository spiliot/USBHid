using System;
using System.Runtime.InteropServices;

namespace UsbHid.USB.Classes.DllWrappers
{
    public static class User32
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr RegisterDeviceNotification(IntPtr hRecipient,IntPtr notificationFilter,Int32 flags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern Boolean UnregisterDeviceNotification(IntPtr handle);
    }
}

