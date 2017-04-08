# UsbHid
A C# project to handle multiple generic HID USB devices

## Description

Since .net doesn't include ready-made classes to talk to generic HID USB devices, wrappers around the windows API are needed. If you're interested in windows store applications, the
relevant WS API has included such functionality, but limits targets to windows store apps (obviously) and at least windows 8.1/10. This project can be used with windows 7 - maybe
even Vista and XP but who cares about them really :)

Inspiration and original code came from the work of Szymon Roslowski, published in a [codeplex article](https://www.codeproject.com/Tips/530836/Csharp-USB-HID-Interface) with the
very permissive [Code Project Open License](https://www.codeproject.com/info/cpol10.aspx). Szymon's code is indeed one of the cleanest out there but is unmaintained for several years
and lucks support for talking to multiple HID devices sharing the same VID/PID (Vendor/Product ID) since only one device (the first discovered) for a given VID/PID can be
instantiated. Also no notification for newly inserted devices is provided, one has to use the cumbersome windows API to get notified or poll continuously.

Instantiating only one device is a problem when using several of the same VID/PID pair devices, usually because you can't afford the (upwards of $5000) payment to USB.org to get your
own VID and have to resort to sharing the _public_ provided HID VID/PID pair offered by [Objective Development][OD].

Since on the job, a major code cleanup is underway to remove unnecessary checks, simplify loops, reduce cyclomatic complexity and create happy paths instead of `else`'s and nested
`if`'s. Cleaned up code is mostly self-documenting so most comments are removed, code style is enforced and `Debug.WriteLine()` statements are kept to the minimum.

## USB discovery and device instantiation in Windows

USB is pretty straight forward for the end user but quite complicated from a hardware/software implementation perspective. I will not attempt to go in depth here, there's excellent
information on the subject in other places. The explanation here covers the basics needed to understand the process of finding and using a device in windows.

Each USB device that serves one purpose has to be identified by a unique VID/PID, i.e. a printer or a joystick. This makes uniquely paired VID/PID devices easy to detect and work
with. If several devices of the same VID/PID are used then they _have_ to have a unique SERIAL number each (i.e. two or more of the same model of printers). Differentiating the
Manufacturer/Product description strings to include a unique identifier will not suffice as these strings are read _after_ identifying a device by VID/PID/SERIAL. The uniqueness
of the VID/PID/SERIAL is not formally enforced in a hard way (i.e. "duplicate" devices being somehow rejected from the bus when connected), so you can get away with it and some other
means to identify duplicate devices is needed. Finally when no SERIAL is used, 0 is assumed, so technically the serial is used all the times.

To solve this problem, windows will generate a unique _device instance path_ string for each USB device found. It also takes the REV (revision) number into account and for those that
are found to share the same VID/PID(/REV)/SERIAL a random, unique string, is inserted in each device's instance path. For HID devices it would look like
`\\\\?\\hid#vid_16c0&pid_27d9#7&62250e9&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}` in code and `HID\VID_16C0&PID_27D9\3&62250E9&0&0000` in device manager.

This string contains a "random" part (in this example above `62250E9`) that is probably derived from a hash of the USB hub/port number the device is connected on, because it remains
the same when you reconnect the device in the same port, but changes when you connect it in a different port. Thus you can't expect to persistently identify devices between ports,
computers or maybe even restarts with it. It should be only used to differentiate each device at runtime.

Using this string to instantiate devices allows them to share a VID/PID(/SERIAL). To discover a device based on i.e. its product description string, the device needs to be opened and
the string descriptor read. If the device matches we can use the device instance path to further identify it in the future. Fortunately this two-step process is performed internally.
To match a device use an applicable Matcher class to look for the device(s) you're after. Matcher classes can be extended from the ones already provided, or created from scratch, by
implementing the `IUsbDeviceMatchable` interface.

## How to use

Use a Matcher class (one that implements the `IUsbDeviceMatchable` interface) on `FindHidDevices()` to get a `List` of `KeyValuePair`s that contain the device instance path as
key and the descriptor strings as value for each matching device. Then use any of the returned device instance path strings to instantiate one or more `UsbHidDevice` classes that
access the actual devices. Currently two matcher classes are provided:

__`VidPidMatcher`__ provides the original matching behavior against a VID/PID pair. Use like:
```
var devices = FindHidDevices(new VidPidMatcher(0xDEAD, 0xBEEF));
```

__`SerialStringMatcher`__ matches against (the start of) the USB descriptor serial string and the public VID/PID pair provided by Objective Development or any given VID/PID pair.

```
var devices = FindHidDevices(new SerialStringMatcher("mydevice.com:"));
//when following the OD guidelines or when using your own VID/PID as: 
//FindHidDevices(new SerialStringMatcher("SERIAL_TO_MATCH", 0xDEAD, 0xBEEF))
```

You can parse the returned list with your own logic. For this example, assuming at least one device is returned from `FindHidDevices()`, then simply instantiate it like:
```
var myHidDevice = new UsbHidDevice(devices[0].Key); //devices[0].Key contains the instance path string of the first device returned
```

You can then immediately use the device:
```
myHidDevice.DataReceived += MyHidDevice_DataReceived; //Subscribe your own handler function for the DataReceived event
myHidDevice.SendCommandMessage(0x10); //Send a command message with payload (0x10)
```
**NOTE:** The events for (new) device connection/disconnection are not yet available in this version. You can instead poll for new devices. Adding this requires a major refactor.

## Disclaimer

I mostly adapted this to serve my _own_ needs. I don't attempt to [cover everyone's use case](https://imgs.xkcd.com/comics/standards.png). If something doesn't work as expected please
file an issue but since I work on this on my spare time, no guaranties on fixing it.

## License

[Code Project Open License](https://www.codeproject.com/info/cpol10.aspx)

[OD]:https://github.com/obdev/v-usb/blob/master/usbdrv/USB-IDs-for-free.txt