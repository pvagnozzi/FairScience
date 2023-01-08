using Android.Hardware.Usb;
using FairScience.Device.Serial.Platforms.Android.Usb;
using Microsoft.Extensions.Logging;

namespace FairScience.Device.Serial.Platforms.Android.Drivers.Cp21xx;

[UsbSerialDriver(UsbId.VENDOR_SILABS, new[]
{
    UsbId.SILABS_CP2102,
    UsbId.SILABS_CP2105,
    UsbId.SILABS_CP2108,
    UsbId.SILABS_CP2110
})]
public class Cp21xxUsbSerialDriver : CommonUsbSerialDriver
{
    public Cp21xxUsbSerialDriver(UsbManager manager, UsbDevice device, ILogger logger) : 
	    base(manager, device, logger, typeof(Cp21xxUsbSerialPortDriver))
    {
    }
}

