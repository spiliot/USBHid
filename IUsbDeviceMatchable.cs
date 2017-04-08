using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UsbHid.USB.Classes;
using UsbHid.USB.Structures;

namespace UsbHid
{
    public interface IUsbDeviceMatchable
    {
        bool BasicMatch(string deviceInstancePath);
        bool DescriptorsMatch(UsbDescriptorStrings descriptorStrings);
    }
}
