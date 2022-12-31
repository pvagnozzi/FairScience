namespace FairScience.Device.Serial.Platforms.Android.Usb;

public interface IUsbSerialDriver : IDisposable
{
    IEnumerable<IUsbSerialPortDriver> GetPorts();
}

