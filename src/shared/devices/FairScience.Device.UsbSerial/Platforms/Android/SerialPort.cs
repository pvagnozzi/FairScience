using FairScience.Device.Serial.Platforms.Android.Usb;
using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
namespace FairScience.Device.Serial;

public class SerialPort : ISerialPort
{
    protected internal SerialPort(
        IUsbSerialPortDriver driver, 
        ILogger logger)
    {
        Driver = driver;
        Logger = logger;
    }

    #region Properties
    public ILogger Logger { get; }
    public string PortName => $"{Driver.UsbDevice.DeviceId}/{Driver.PortNumber}";
    public bool IsOpen => Driver.IsOpen;
    public SerialPortParameters Parameters { get; private set; }
    public IUsbSerialPortDriver Driver { get; }
    #endregion

    #region Methods
    public virtual void Open(SerialPortParameters parameters = null)
    {
        parameters ??= new SerialPortParameters();
        Logger?.LogDebug("SerialPort {portName}: open {parameters}", PortName, parameters);

        if (IsOpen)
        {
            Logger?.LogDebug("SerialPort {portName}: already open", PortName);
            return;
        }

        Parameters = parameters;
        Driver.Open(Driver.UsbManager, parameters);
    }

    public virtual int Read(byte[] data)
    {
        Logger?.LogDebug("SerialPort {portName}: read", PortName);

        if (!IsOpen)
        {
            throw new InvalidOperationException("Port is not open");
        }

        return Driver.Read(data);
    }

    public virtual int Write(byte[] data)
    {
        Logger?.LogDebug("SerialPort {portName}: write", PortName);

        if (!IsOpen)
        {
            throw new InvalidOperationException("Port is not open");
        }

        return Driver.Write(data);
    }
    
    public virtual void Close()
    {
        Logger?.LogDebug("SerialPort {portName}: close", PortName);

        if (!IsOpen)
        {
            return;
        }

        Driver.Close();
    }

    public virtual void Dispose()
    {
        Driver?.Dispose();
        GC.SuppressFinalize(this);
    }
    #endregion
}
