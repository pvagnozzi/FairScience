using Android.Hardware.Usb;
using FairScience.Device.Serial.Platforms.Android.Usb;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using FairScience.Reflection;

namespace FairScience.Device.Serial.Platforms.Android.Drivers;

public abstract class CommonUsbSerialDriver : UsbSerialDriver
{

    protected CommonUsbSerialDriver(
        UsbManager manager,
	    UsbDevice device, 
	    ILogger logger, 
	    Type serialPortDriverType) : 
        base(manager, device, logger)
    {
        PortCreator = serialPortDriverType.BuildConstructor<UsbManager, UsbDevice, int, ILogger, IUsbSerialPortDriver>();
    }

    private Func<UsbManager, UsbDevice, int, ILogger, IUsbSerialPortDriver> PortCreator { get; }


    protected override IUsbSerialPortDriver GetPort(UsbManager manager, UsbDevice device, int port, ILogger logger) =>
        PortCreator(manager, device, port, logger);
}
