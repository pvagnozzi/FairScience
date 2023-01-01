using Android.Hardware.Usb;
using FairScience.Device.Serial.Platforms.Android.Usb;
using Microsoft.Extensions.Logging;

namespace FairScience.Device.Serial.Platforms.Android.Drivers.STM32;

[UsbSerialDriver(UsbId.VENDOR_STM, new[] { UsbId.STM32_STLINK, UsbId.STM32_VCOM })]
public class STM32UsbSerialDriver : CommonUsbSerialDriver
{
    public STM32UsbSerialDriver(UsbDevice device, ILogger logger) : base(device, logger,
        typeof(STM32UsbSerialPortDriver))
    {
    }
}

