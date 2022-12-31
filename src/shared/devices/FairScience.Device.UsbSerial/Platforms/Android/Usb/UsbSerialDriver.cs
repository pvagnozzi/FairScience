using Android.Hardware.Usb;
using Microsoft.Extensions.Logging;

namespace FairScience.Device.Serial.Platforms.Android.Usb;

public abstract class UsbSerialDriver : IUsbSerialDriver
{
    private readonly List<IUsbSerialPortDriver> _ports = new();

    protected UsbSerialDriver(UsbDevice device, ILogger logger)
    {
        Device = device;
        Logger = logger;
    }

    public UsbDevice Device { get; }

    public ILogger Logger { get; }

    public IEnumerable<IUsbSerialPortDriver> GetPorts()
    {
        if (!_ports.Any())
        {
            _ports.AddRange(ScanPorts());
        }

        return _ports;
    }


    public virtual void Dispose()
    {
        foreach (var port in _ports)
        {
            port.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    protected virtual IEnumerable<IUsbSerialPortDriver> ScanPorts()
    {
        for (var port = 0; port < Device.InterfaceCount; port++)
        {
            yield return GetPort(Device, port, Logger);
        }
    }

    protected abstract IUsbSerialPortDriver GetPort(UsbDevice device, int port, ILogger logger);
}

