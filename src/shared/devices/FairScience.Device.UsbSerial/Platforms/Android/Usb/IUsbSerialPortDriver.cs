using Android.Hardware.Usb;

namespace FairScience.Device.Serial.Platforms.Android.Usb;

public interface IUsbSerialPortDriver : IDisposable
{
    UsbDevice Device { get; }
    UsbDeviceConnection Connection { get; }
    bool IsOpen { get; }
    int PortNumber { get; }


    void Open(UsbManager usbManager, SerialPortParameters parameters);
    void Close();
    int Read(byte[] dest);
    int Write(byte[] src);

    bool GetCD();
    bool GetCTS();
    bool GetDSR();
    bool GetDTR();
    void SetDTR(bool value);
    bool GetRI();
    bool GetRTS();
    void SetRTS(bool value);
}

