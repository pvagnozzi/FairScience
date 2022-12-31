using Android.Content.Res;
using Android.Hardware.Usb;
using FairScience.Device.Serial.Platforms.Android.Usb;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace FairScience.Device.Serial.Platforms.Android;

public sealed class SerialPortProvider : ISerialPortProvider
{
    private readonly IDictionary<string, ISerialPort> _serialPorts = new ConcurrentDictionary<string, ISerialPort>();
    
    public SerialPortProvider(UsbManager usbManager, ILogger logger = null)
    {
        UsbManager = usbManager;
        Logger = logger;
    }

    public UsbManager UsbManager { get; }

    public ILogger Logger { get; }

    public IList<string> GetPortNames()
    {
        if (_serialPorts.Any())
        {
            return _serialPorts.Keys.ToList();
        }

        var drivers = ScanDrivers();

        foreach (var portDriver in drivers.SelectMany(x => x.GetPorts()))
        {
            var port = new SerialPort(portDriver, Logger);
            _serialPorts.Add(port.PortName, port);
        }

        return _serialPorts.Keys.ToList();
    }

    public ISerialPort GetSerialPort(string portName)
    {
        if (!_serialPorts.TryGetValue(portName, out var port))
        {
            throw new Resources.NotFoundException($"SerialPort {portName}");
        }

        return port;
    }

    private IEnumerable<IUsbSerialDriver> ScanDrivers()
    {
        var prober = new UsbSerialProber(Logger);
        return prober.FindAllDrivers(UsbManager);
    }
}

