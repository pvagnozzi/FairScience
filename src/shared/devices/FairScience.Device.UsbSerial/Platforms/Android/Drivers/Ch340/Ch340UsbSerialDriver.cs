using Android.Hardware.Usb;
using FairScience.Device.Serial.Platforms.Android.Usb;
using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
namespace FairScience.Device.Serial.Platforms.Android.Drivers.Ch43x;


[UsbSerialDriver(UsbId.VENDOR_QINHENG, new[] { UsbId.QINHENG_HL340 })]
public class Ch430UsbSerialDriver : CommonUsbSerialDriver
{
    public Ch430UsbSerialDriver(UsbManager manager, UsbDevice device, ILogger logger) : 
	    base(manager, device, logger, typeof(Ch340UsbSerialPortDriver))
    {
    }
}

