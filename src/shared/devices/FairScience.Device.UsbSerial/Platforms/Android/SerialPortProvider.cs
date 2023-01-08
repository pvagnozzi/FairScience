using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using FairScience.Device.Serial.Platforms.Android.Usb;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

// ReSharper disable once CheckNamespace
namespace FairScience.Device.Serial;

public class SerialPortProvider : ISerialPortProvider
{
    private readonly IDictionary<string, ISerialPort> _serialPorts = new ConcurrentDictionary<string, ISerialPort>();

    public SerialPortProvider(Activity activity = null, ILogger logger = null)
    {
        activity ??= Platform.CurrentActivity;
        if (activity is null)
        {
            throw new ArgumentException("No current activity available");
        }

        var usbManager = (UsbManager)activity.GetSystemService (Context.UsbService);
        UsbManager = usbManager;
        Logger = logger;
    }
    
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
            throw new KeyNotFoundException($"SerialPort {portName}");
        }

        return port;
    }

    private IEnumerable<IUsbSerialDriver> ScanDrivers()
    {
        var prober = new UsbSerialProber(Logger);
        return prober.Scan(UsbManager);
    }
}

