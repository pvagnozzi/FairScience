using Android.Hardware.Usb;
using Microsoft.Extensions.Logging;

namespace FairScience.Device.Serial.Platforms.Android.Drivers.Ftdi;

public class FtdiUsbSerialDriver : CommonUsbSerialDriver
{
    public FtdiUsbSerialDriver(UsbDevice device, ILogger logger) : base(device, logger, typeof(FtdiUsbSerialPortDriver))
    {
    }
}

