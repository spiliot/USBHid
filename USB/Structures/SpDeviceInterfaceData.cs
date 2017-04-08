using System;
using System.Runtime.InteropServices;

namespace UsbHid.USB.Structures
{
    /// <see cref="https://msdn.microsoft.com/en-us/library/windows/hardware/ff552342(v=vs.85).aspx"/>
    [StructLayout(LayoutKind.Sequential)]
    public struct SpDeviceInterfaceData
    {
        public Int32 cbSize;
        public Guid InterfaceClassGuid;
        public Int32 Flags;
        public IntPtr Reserved;
    }
}