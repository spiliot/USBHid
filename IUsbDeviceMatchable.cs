using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UsbHid.USB.Structures;

namespace UsbHid
{
    public interface IUsbDeviceMatchable
    {
        bool MatchVidPid(DeviceInformationStructure device);
        bool MatchExtendedInformation(DeviceInformationStructure device);
    }
}
