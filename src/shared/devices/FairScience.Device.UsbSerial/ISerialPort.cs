namespace FairScience.Device.Serial;

public interface ISerialPort : IDisposable
{
    bool IsOpen { get; }
    void Open(SerialPortParameters parameters);
    void Close();
    int Read(byte[] data);
    int Write(byte[] data);
}

