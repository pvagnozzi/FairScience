namespace FairScience.Device.Serial.Platforms.Android.Usb;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class UsbSerialDriverAttribute : Attribute
{
    public UsbSerialDriverAttribute(int vendorId, int[] deviceIds) =>
        DriverInfo = new(vendorId, deviceIds);
    
    public UsbSerialDriverInfo DriverInfo { get; }
}

