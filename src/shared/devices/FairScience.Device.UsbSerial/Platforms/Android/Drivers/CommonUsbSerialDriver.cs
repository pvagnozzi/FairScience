using Android.Hardware.Usb;
using FairScience.Device.Serial.Platforms.Android.Usb;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace FairScience.Device.Serial.Platforms.Android.Drivers;

public abstract class CommonUsbSerialDriver : UsbSerialDriver
{

    protected CommonUsbSerialDriver(UsbDevice device, ILogger logger, Type serialPortDriverType) : 
        base(device, logger)
    {
        PortCreator = BuildPortCreator(serialPortDriverType);
    }

    private Func<UsbDevice, int, ILogger, IUsbSerialPortDriver> PortCreator { get; }

    private static Func<UsbDevice, int, ILogger, IUsbSerialPortDriver> BuildPortCreator(Type driverType)
    {
        var ctor = driverType.GetConstructor(new[] { typeof(UsbDevice), typeof(int), typeof(ILogger) });
        if (ctor is null)
        {
            throw new ArgumentException($"{driverType.FullName} has no valid constructors as UsbSerialPortDriver");
        }

        var deviceParameter = Expression.Parameter(typeof(UsbDevice));
        var portParameter = Expression.Parameter(typeof(int));
        var loggerParameter = Expression.Parameter(typeof(ILogger));
        var parameters = new[] { deviceParameter, portParameter, loggerParameter };

        var newExpression = Expression.New(ctor, parameters.Cast<Expression>());
        var lambda = Expression.Lambda<Func<UsbDevice, int, ILogger, IUsbSerialPortDriver>>(newExpression, parameters);
        return lambda.Compile();
    }

    protected override IUsbSerialPortDriver GetPort(UsbDevice device, int port, ILogger logger) =>
        PortCreator(device, port, logger);
}
