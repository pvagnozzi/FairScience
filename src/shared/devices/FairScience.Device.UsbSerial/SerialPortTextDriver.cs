using System.Text;

namespace FairScience.Device.Serial;

public class SerialPortTextDriver : IDisposable
{
	public SerialPortTextDriver(ISerialPort serialPort, Encoding encoding = null, string newLine = null)
	{
		SerialPort = serialPort;
		Encoding = encoding ?? Encoding.UTF8;
		NewLine = newLine ?? Environment.NewLine;
	}

	public string NewLine { get; }
	public ISerialPort SerialPort { get; }
	public Encoding Encoding { get; }

	public string Read(int len)
	{
		var buffer = new byte[len];
		SerialPort.Read(buffer);
		var str = Encoding.GetString(buffer);
		return str;
	}

	public void Write(string value)
	{
		var content = Encoding.GetBytes(value);
		SerialPort.Write(content);
	}

	public void WriteLine(string value) =>
		Write($"{value}{NewLine}");

	public void Dispose()
	{
		SerialPort.Dispose();
		GC.SuppressFinalize(this);
	}
}

