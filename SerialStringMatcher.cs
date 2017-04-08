using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UsbHid.USB.Classes;
using UsbHid.USB.Structures;

namespace UsbHid
{
    class SerialStringMatcher : VidPidMatcher, IUsbDeviceMatchable
    {
        private readonly string SerialString;

        public SerialStringMatcher(string SerialToMatch, uint Vid = 0x16c0, uint Pid = 0x27d9) : base(Vid, Pid)
        {
            this.SerialString = SerialToMatch;
        }

        override public bool DescriptorsMatch(UsbDescriptorStrings descriptorStrings)
        {
            return descriptorStrings.Serial.StartsWith(SerialString);
        }
    }
}
