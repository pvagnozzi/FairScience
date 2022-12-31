using Android.Hardware.Usb;
using Java.Lang;
using Java.Lang.Reflect;
using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
namespace FairScience.Device.Serial.Platforms.Android.Usb;

internal sealed class UsbSerialProber
{
    public UsbSerialProber(ILogger logger)
    {
        Logger = logger;
    }

    public ILogger Logger { get; }

    public IList<IUsbSerialDriver> FindAllDrivers(UsbManager usbManager) => usbManager.DeviceList?.Values
        .Select(ProbeDevice).Where(driver => driver != null).ToList();

    public IUsbSerialDriver ProbeDevice(UsbDevice usbDevice)
    {
        var vendorId = usbDevice.VendorId;
        var productId = usbDevice.ProductId;

        var driverClass = typeof(IUsbSerialDriver);
        if (driverClass is null)
        {
            throw new RuntimeException($"UsbSerialPort Driver {vendorId}/{productId} not found");
        }

        try
        {
            return (IUsbSerialDriver)Activator.CreateInstance(driverClass, usbDevice, Logger);
        }
        catch (NoSuchMethodException e)
        {
            throw new RuntimeException(e);
        }
        catch (IllegalArgumentException e)
        {
            throw new RuntimeException(e);
        }
        catch (InstantiationException e)
        {
            throw new RuntimeException(e);
        }
        catch (IllegalAccessException e)
        {
            throw new RuntimeException(e);
        }
        catch (InvocationTargetException e)
        {
            throw new RuntimeException(e);
        }
    }

}
