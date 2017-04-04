using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UsbHid.USB.Classes
{
    public class UsbDescriptorStrings
    {
        public readonly string Manufacturer;
        public readonly string Product;
        public readonly string Serial;

        public UsbDescriptorStrings(string Manufacturer, string Product, string Serial)
        {
            this.Manufacturer = Manufacturer;
            this.Product = Product;
            this.Serial = Serial;
        }
    }
}
