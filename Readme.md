# UsbHid
A C# project to handle multiple generic HID USB devices

## Description

Since .net doesn't include ready made classes to talk to generic HID USB devices, wrappers around the windows API are needed. The newer windows store API included such functionality but limits targets to at least windows 8.1/10 and windows store applications. This project can be used with windows 7 and maybe even Vista but who cares really :)

Code and inspiration came from work of Szymon Roslowski published in a [codeplex article](https://www.codeproject.com/Tips/530836/Csharp-USB-HID-Interface) with the very permissive [Code Project Open License](https://www.codeproject.com/info/cpol10.aspx).

Szymon's code is indeed one of the cleanest out there but is unmaintained for several years and lucks support for talking to multiple HID devices sharing the same VID/PID (Vendor/Product ID) since only one device (the first discovered) for a given VID/PID can be instantiated.

This is a problem when using several of the same devices, or when you can't afford the upwards of $5000 payment to USB.org to get your own VID and share the _public_ HID VID/PID pair offered by [Objective Development][OD].

Since on the job, a major code cleanup is underway to remove unnecessary checks, simplify loops, reduce cyclomatic complexity and create happy paths instead of `else`'s and nested `if`'s. Cleaned up code is mostly self-documenting so most comments are removed, code style is enforced and `Debug.WriteLine()` statements are kept to the minimum.

## How to use

Instantiate the `UsbHidDevice` class with a class that implements the `IUsbDeviceMatchable` interface. Currently two matcher classes are provided:

__`VidPidMatcher`__ provides the original matching behaviour against a VID/PID pair. Use like: `var myHidDevice = new UsbHidDevice(new VidPidMatcher(0xDEAD, 0xBEEF))`.

__`SerialStringMatcher`__ matches against (the start of) the USB descriptor serial string and the public VID/PID pair provided by Objective Development or any given VID/PID pair.
Use like: `var myHidDevice = new UsbHidDevice(new SerialStringMatcher("mydevice.com:")` (according to Objective Development's [guidelines][OD]) or as `var myHidDevice = new UsbHidDevice(new SerialStringMatcher("SERIAL_TO_MATCH", 0xDEAD, 0xBEEF))` when using your own VID/PID.

## Disclaimer

I mostly adapted this to serve my _own_ needs. I don't attempt to [cover everyone's use case](https://imgs.xkcd.com/comics/standards.png). If something doesn't work as expected please file an issue but since I work on this on my spare time, no guarranties on fixing it.

## License

[Code Project Open License](https://www.codeproject.com/info/cpol10.aspx)

[OD]:https://github.com/obdev/v-usb/blob/master/usbdrv/USB-IDs-for-free.txt