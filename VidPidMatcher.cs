using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        public bool MatchVidPid(DeviceInformationStructure device)
        {
            bool matchesVid = (device.Attributes.VendorID == Vid);
            bool matchesPid = (device.Attributes.ProductID == Pid);

            return (matchesVid && matchesPid);
        }

        public virtual bool MatchExtendedInformation(DeviceInformationStructure device)
        {
            return true;
        }
    }
}
