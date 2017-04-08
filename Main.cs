using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UsbHid;
using UsbHid.USB.Classes;

namespace UsbHid
{
    public static class Main
    {
        static class Program
        {
            /// <summary>
            /// The main entry point for the application.
            /// </summary>
            [STAThread]
            static void Main()
            {
                var devices = DeviceDiscovery.FindHidDevices(new SerialStringMatcher("aimforfs.eu:RMP"));
                var device = new UsbHidDevice(devices[0].Key);

                var command = new USB.Classes.Messaging.CommandMessage(0x20, new byte[] {
                    16,
                    0, (1<<0)+(1<<3)+(1<<6)+(1<<7)+(1<<4),
                    1, 0x0,
                    2, 0x0,
                    3, 1 << 1,
                    4, (1<<0)+(1<<2)+(1<<3)+(1<<4)+(1<<6)+(1<<7),
                    5, (1<<3)+(1<<5),
                    6, 0x0,
                    7, (1<<0)+(1<<2)+(1<<4)+(1<<5)+(1<<7),
                    8, (1<<4)+(1<<3)+(1<<5)+(1<<6),
                    9, (1<<0)+(1<<1)+(1<<4)+(1<<3)+(1<<5)+(1<<7),
                    10, (1<<0)+(1<<1)+(1<<4)+(1<<3)+(1<<5)+(1<<7),
                    11, (1<<4)+(1<<3)+(1<<5)+(1<<6),
                    12, (1<<0)+(1<<2)+(1<<4)+(1<<5)+(1<<7),
                    13, (1<<0)+(1<<3)+(1<<6)+(1<<7)+(1<<4),
                    14, (1<<3)+(1<<5),
                    15, (1<<0)+(1<<2)+(1<<3)+(1<<4)+(1<<6)+(1<<7)
                });
                device.SendMessage(command);

                System.Diagnostics.Debug.WriteLine(devices);
            }
        }
    }
}
