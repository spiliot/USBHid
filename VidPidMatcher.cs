using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UsbHid.USB.Classes;
using UsbHid.USB.Structures;

namespace UsbHid
{
    class VidPidMatcher : IUsbDeviceMatchable
    {
        public readonly uint Vid;
        public readonly uint Pid;

        public VidPidMatcher(uint Vid, uint Pid)
        {
            this.Vid = Vid;
            this.Pid = Pid;
        }

        public bool BasicMatch(string deviceInstancePath)
        {
            if (deviceInstancePath.IndexOf("#vid_" + Vid.ToString("x4") + "&") == -1) return false;
            if (deviceInstancePath.IndexOf("&pid_" + Pid.ToString("x4") + "#") == -1) return false;
            return true;
        }

        public virtual bool DescriptorsMatch(UsbDescriptorStrings descriptorStrings)
        {
            return true;
        }
    }
}
