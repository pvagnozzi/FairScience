/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.Hardware.Usb;

namespace FairScience.Device.UsbSerial.Platforms.Android.Drivers;

public abstract class UsbSerialPort
{
    public const int DATABITS_5 = 5;
    public const int DATABITS_6 = 6;
    public const int DATABITS_7 = 7;
    public const int DATABITS_8 = 8;

    public const int FLOWCONTROL_NONE = 0;
    public const int FLOWCONTROL_RTSCTS_IN = 1;
    public const int FLOWCONTROL_RTSCTS_OUT = 2;
    public const int FLOWCONTROL_XONXOFF_IN = 4;
    public const int FLOWCONTROL_XONXOFF_OUT = 8;

    public const int PARITY_NONE = 0;
    public const int PARITY_ODD = 1;
    public const int PARITY_EVEN = 2;
    public const int PARITY_MARK = 3;
    public const int PARITY_SPACE = 4;

    public const int STOPBITS_1 = 1;
    public const int STOPBITS_1_5 = 3;
    public const int STOPBITS_2 = 2;

    
    protected UsbSerialPort(IUsbSerialDriver driver, int portNumber)
    {
        Driver = driver;
        PortNumber = portNumber;
    }

    public IUsbSerialDriver Driver { get; }
    public int PortNumber { get; }
    public abstract void Open(UsbDeviceConnection connection);
    public abstract void Close();
    public abstract int Read(byte[] dest, int timeoutMillis);
    public abstract int Write(byte[] src, int timeoutMillis);

    public abstract void SetParameters(
        int baudRate, int dataBits, StopBits stopBits, Parity parity);

    public abstract bool GetCD();
    public abstract bool GetCTS();
    public abstract bool GetDSR();
    public abstract bool GetDTR();
    public abstract void SetDTR(bool value);

    public abstract bool GetRI();
    public abstract bool GetRTS();
    public abstract void SetRTS(bool value);
    public abstract bool PurgeHwBuffers(bool flushRX, bool flushTX);
}
