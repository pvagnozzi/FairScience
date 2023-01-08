using Android.Hardware.Usb;
using Microsoft.Extensions.Logging;

namespace FairScience.Device.Serial.Platforms.Android.Usb;

public abstract class UsbSerialDriver : IUsbSerialDriver
{
    private readonly List<IUsbSerialPortDriver> _ports = new();

    protected UsbSerialDriver(UsbManager manager, UsbDevice usbDevice, ILogger logger)
    {
	    UsbManager = manager;
        UsbDevice = usbDevice;
        Logger = logger;
    }

    public UsbManager UsbManager { get; }

    public UsbDevice UsbDevice { get; }

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
        for (var port = 0; port < UsbDevice.InterfaceCount; port++)
        {
            yield return GetPort(UsbManager, UsbDevice, port, Logger);
        }
    }

    protected abstract IUsbSerialPortDriver GetPort(UsbManager manager, UsbDevice device, int port, ILogger logger);
}

