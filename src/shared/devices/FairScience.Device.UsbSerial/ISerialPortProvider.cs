namespace FairScience.Device.Serial;

public interface ISerialPortProvider
{
    IList<string> GetPortNames();

    ISerialPort GetSerialPort(string portName);
}

