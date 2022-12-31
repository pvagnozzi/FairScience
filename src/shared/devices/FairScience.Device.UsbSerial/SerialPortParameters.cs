namespace FairScience.Device.Serial;

public record SerialPortParameters(
    int BaudRate = 115200, 
    DataBits DataBits = DataBits.Bits8,
    StopBits StopBits = StopBits.One, 
    Parity Partity = Parity.None, 
    FlowControl FlowControl = FlowControl.None,
    int ReadTimeout = 1000,
    int WriteTimeout = 1000,
    int ReadBufferSize = 16 * 1024,
    int WriteBufferSize = 16 * 1024
);
