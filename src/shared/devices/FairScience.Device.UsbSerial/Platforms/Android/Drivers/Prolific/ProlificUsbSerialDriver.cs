using Android.Hardware.Usb;
using FairScience.Device.Serial.Platforms.Android.Usb;
using Microsoft.Extensions.Logging;

namespace FairScience.Device.Serial.Platforms.Android.Drivers.Prolific;

[UsbSerialDriver(UsbId.VENDOR_PROLIFIC, new[]
{
    UsbId.PROLIFIC_PL2303,
    UsbId.PROLIFIC_PL2303GC,
    UsbId.PROLIFIC_PL2303GB,
    UsbId.PROLIFIC_PL2303GT,
    UsbId.PROLIFIC_PL2303GL,
    UsbId.PROLIFIC_PL2303GE,
    UsbId.PROLIFIC_PL2303GS

})]
public class ProlificUsbSerialDriver : CommonUsbSerialDriver
{
    public ProlificUsbSerialDriver(UsbManager manager, UsbDevice device, ILogger logger) : 
	    base(manager, device, logger, typeof(ProlificUsbSerialPortDriver))
    {
    }
}

