using Android.Hardware.Usb;
using FairScience.Device.Serial.Platforms.Android.Usb;
using Microsoft.Extensions.Logging;

namespace FairScience.Device.Serial.Platforms.Android.Drivers;

public abstract class CommonUsbSerialPortDriver : UsbSerialPortDriver
{
    public const int DEFAULT_READ_BUFFER_SIZE = 16 * 1024;
    public const int DEFAULT_WRITE_BUFFER_SIZE = 16 * 1024;

    private readonly object _readBufferLock = new();
    private readonly object _writeBufferLock = new();
    private byte[] _readBuffer = Array.Empty<byte>();
    private byte[] _writeBuffer = Array.Empty<byte>();

    protected CommonUsbSerialPortDriver(
        UsbDevice device,
        int portNumber,
        ILogger logger) :
        base(device, portNumber, logger)
    {
    }

    #region Properties
    protected UsbEndpoint ReadEndPoint { get; private set; }
    protected UsbEndpoint WriteEndPoint { get; private set;  }
    #endregion

    #region Methods
    public override void Open(UsbManager usbManager, SerialPortParameters parameters)
    {
        if (IsOpen)
        {
            return;
        }

        var connection = OpenConnection(usbManager);
        SetParameters(connection, parameters);
        Connection = connection;
        SetInterfaces(Device);

        lock (_readBufferLock)
        {
            _readBuffer = new byte[parameters.ReadBufferSize > 0 ? parameters.ReadBufferSize : DEFAULT_READ_BUFFER_SIZE];
        }

        lock (_writeBufferLock)
        {
            _writeBuffer = new byte[parameters.WriteBufferSize > 0 ? parameters.WriteBufferSize : DEFAULT_WRITE_BUFFER_SIZE];
        }
    }

    public override void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        CloseConnection();
    }

    public override int Read(byte[] dest)
    {
        var len = ReadBufferFromDevice(dest.Length);
        _readBuffer.CopyTo(dest, len);
        return len;
    }

    public override int Write(byte[] src)
    {
        src.CopyTo(_writeBuffer, src.Length);
        return WriteBufferToDevice(src.Length);
    }

    #endregion

    #region Protected Methods

    protected void SetReadEndPoint(UsbEndpoint usbEndpoint)
    {
        lock (_readBufferLock)
        {
            ReadEndPoint = usbEndpoint;
        }
    }

    protected void SetWriteEndPoint(UsbEndpoint usbEndpoint)
    {
        lock (_readBufferLock)
        {
            WriteEndPoint = usbEndpoint;
        }
    }

    protected virtual int ReadBufferFromDevice(int len)
    {
        var result = new List<byte>();

        var toRead = len;
        while(toRead > 0)
        {
            var buffer = new byte[len];
            var read = ReadFromDevice(buffer);
            toRead -= read;
            result.AddRange(buffer);
        }

        return CopyToReadBuffer(result.ToArray(), _readBuffer);
    }

    protected virtual int WriteBufferToDevice(int len)
    {
        int offset ;
        for (offset = 0; offset < len;)
        {

            var buffer = CopyFromWriteBuffer(_writeBuffer, offset, len);
            var written = WriteToDevice(buffer);
            offset += written;
        }

        return offset;
    }

    protected virtual int ReadFromDevice(byte[] buffer) =>
        Connection.BulkTransfer(ReadEndPoint, buffer, buffer.Length, 10);

    protected virtual int WriteToDevice(byte[] buffer) =>
        Connection.BulkTransfer(WriteEndPoint, buffer, buffer.Length, 10);

    protected virtual int CopyToReadBuffer(byte[] source, byte[] destination)
    {
        Array.Copy(source, destination, source.Length);
        return source.Length;
    }

    protected virtual byte[] CopyFromWriteBuffer(byte[] source, int offset, int length)
    {
        var toWrite = length - offset;
        var buffer = new byte[toWrite];
        Buffer.BlockCopy(source, offset, buffer, 0, toWrite);
        return buffer;
    }

    protected virtual UsbDeviceConnection OpenConnection(UsbManager usbManager)
    {
        var connection = usbManager.OpenDevice(Device);

        return connection;
    }

    protected virtual void CloseConnection()
    {
        Connection.Close();
        Connection = null;
    }

    protected abstract void SetInterfaces(UsbDevice device);

    protected (UsbInterface Interface, int Index) FindInterface(UsbDevice device, Func<UsbInterface, bool> filter)
    {
        for (var index = 0; index < device.InterfaceCount; index++)
        {
            var usbInterface = device.GetInterface(index);
            if (!filter(usbInterface))
            {
                continue;
            }

            if (ClaimInterface(usbInterface))
            {
                return (usbInterface, index);
            }

            throw new UsbSerialRuntimeException($"Error during claim usb interface {usbInterface}");
        }

        return (null, -1);
    }

    protected virtual bool ClaimInterface(UsbInterface usbInterface) =>
        Connection.ClaimInterface(usbInterface, true);

    protected abstract void SetParameters(
        UsbDeviceConnection connection, 
        SerialPortParameters parameters);

    protected virtual int ControlTransfer(
        UsbAddressing requestType, 
        int request,
        int value,
        int index,
        byte[] buffer = null,
        int length = -1,
        int timeout = -1)
    {
        var result = Connection.ControlTransfer(
            requestType, 
            request,
            value, 
            index, 
            buffer, 
            length, 
            timeout);

        return result;
    }

    #endregion
}
