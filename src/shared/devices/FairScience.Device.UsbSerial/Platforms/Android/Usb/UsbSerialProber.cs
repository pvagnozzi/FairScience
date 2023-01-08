using Android.Hardware.Usb;
using Java.Lang;
using Java.Lang.Reflect;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using Org.Xmlpull.V1.Sax2;

// ReSharper disable once CheckNamespace
namespace FairScience.Device.Serial.Platforms.Android.Usb;

internal sealed class UsbSerialProber
{
    
    public UsbSerialProber(ILogger logger)
    {
        Logger = logger;
        RegisterDriver(GetType().Assembly);
    }

    public ILogger Logger { get; }

    public IDictionary<UsbSerialPortDriverId, Type> Drivers { get; } =
        new ConcurrentDictionary<UsbSerialPortDriverId, Type>();

    public void RegisterDriver(int vendorId, int deviceId, Type driverType)
    {
        var key = new UsbSerialPortDriverId(vendorId, deviceId);

        if (Drivers.ContainsKey(key))
        {
            return;
        }

        Drivers.Add(key, driverType);
    }
        

    public void RegisterDriver(UsbSerialDriverInfo driverInfo, Type driverType)
    {
        foreach (var productId in driverInfo.DeviceIds)
        {
            RegisterDriver(driverInfo.VendorId, productId, driverType);
        }
    }

    public void RegisterDriver(Type driverType)
    {
        var attributes = driverType.GetCustomAttributes<UsbSerialDriverAttribute>().ToArray();
        if (!attributes.Any())
        {
            throw new ArgumentException($"{driverType.FullName} has no UsbSerialDriverAttribute");
        }

        foreach (var attribute in attributes)
        {
            RegisterDriver(attribute.DriverInfo, driverType);
        }
    }

    public void RegisterDriver(Assembly assembly)
    {
        var driverTypes = assembly.GetTypes()
            .Where(x => x.IsSubclassOf(typeof(UsbSerialDriver)) && !x.IsAbstract).ToArray();

        foreach (var driverType in driverTypes)
        {
            RegisterDriver(driverType);
        }
    }

    public IList<IUsbSerialDriver> Scan(UsbManager usbManager) =>
        usbManager.DeviceList?.Select(device => ProbeDevice(usbManager, device.Value)).Where(driver => driver is not null).ToList();
    
    
    public IUsbSerialDriver ProbeDevice(UsbManager usbManager, UsbDevice usbDevice)
    {
        //usbManager.RequestPermission(usbDevice, null);
        var vendorId = usbDevice.VendorId;
        var productId = usbDevice.ProductId;

        var driverKey = new UsbSerialPortDriverId(vendorId, productId);

        if (!Drivers.TryGetValue(driverKey, out var driverType))
        {
            throw new NotSupportedException($"UsbSerialPort Driver {vendorId}/{productId} not found");
        }

        try
        {
            return (IUsbSerialDriver)Activator.CreateInstance(driverType, usbManager, usbDevice, Logger);
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
