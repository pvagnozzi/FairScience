using Android.Hardware.Usb;
using FairScience.Device.Serial.Platforms.Android.Usb;
using Microsoft.Extensions.Logging;

namespace FairScience.Device.Serial.Platforms.Android.Drivers.Ftdi;

[UsbSerialDriver(UsbId.VENDOR_FTDI, 
    new[]
    {
        UsbId.FTDI_FT232R,
        UsbId.FTDI_FT232H,
        UsbId.FTDI_FT2232H,
        UsbId.FTDI_FT4232H,
        UsbId.FTDI_FT231X,
    }
)]
public class STM32UsbSerialDriver : CommonUsbSerialDriver
{
    public STM32UsbSerialDriver(UsbDevice device, ILogger logger) : base(device, logger,
        typeof(FtdiUsbSerialPortDriver))
    {
    }
}

