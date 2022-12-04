/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.Hardware.Usb;

namespace FairScience.Device.UsbSerial.Platforms.Android.Drivers;

public abstract class CommonUsbSerialPort : UsbSerialPort
{
    public const int DEFAULT_READ_BUFFER_SIZE = 16 * 1024;
    public const int DEFAULT_WRITE_BUFFER_SIZE = 16 * 1024;

    protected UsbDevice mDevice;
    protected int mPortNumber;

    // non-null when open()
    protected UsbDeviceConnection Connection { get; private set; }

    // check if connection is still available
    public bool HasConnection => Connection != null;

    protected readonly object ReadBufferLock = new();
    protected readonly object WriteBufferLock = new();

    protected byte[] ReadBuffer { get; set; }

    protected byte[] WriteBuffer { get; set; }

    protected CommonUsbSerialPort(UsbDevice device, int portNumber, IUsbSerialDriver driver) :
        base(driver, portNumber)
    {
        mDevice = device;
        mPortNumber = portNumber;

        ReadBuffer = new byte[DEFAULT_READ_BUFFER_SIZE];
        WriteBuffer = new byte[DEFAULT_WRITE_BUFFER_SIZE];
    }
    public override string ToString() => $"<{this.GetType().Name} device_name={mDevice.DeviceName} device_id={mDevice.DeviceId} port_number={mPortNumber}>";


    public abstract override void Open(UsbDeviceConnection connection);
    public abstract override void Close();
    public abstract override int Read(byte[] dest, int timeoutMillis);
    public abstract override int Write(byte[] src, int timeoutMillis);
    public abstract override void SetParameters(
        int baudRate, int dataBits, StopBits stopBits, Parity parity);
    public abstract override bool GetCD();
    public abstract override bool GetCTS();
    public abstract override bool GetDSR();
    public abstract override bool GetDTR();
    public abstract override void SetDTR(bool value);
    public abstract override bool GetRI();
    public abstract override bool GetRTS();
    public abstract override void SetRTS(bool value);
    public override bool PurgeHwBuffers(bool flushReadBuffers, bool flushWriteBuffers) => 
        !flushReadBuffers && !flushWriteBuffers;

    protected void SetConnection(UsbDeviceConnection connection = null) => Connection = connection;
}
