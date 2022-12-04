/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.Hardware.Usb;
using Java.Lang;
using Java.Lang.Reflect;

namespace FairScience.Device.UsbSerial.Platforms.Android.Drivers;

public sealed class UsbSerialProber
{
    private readonly ProbeTable _probeTable;

    public UsbSerialProber(ProbeTable probeTable)
    {
        _probeTable = probeTable;
    }

    public static UsbSerialProber GetDefaultProber()
    {
        return new UsbSerialProber(GetDefaultProbeTable());
    }

    public static ProbeTable DefaultProbeTable => GetDefaultProbeTable();

    public static ProbeTable GetDefaultProbeTable()
    {
        var probeTable = new ProbeTable();
        probeTable.AddDriver(typeof(CdcAcmSerialDriver));
        probeTable.AddDriver(typeof(Cp21xxSerialDriver));
        probeTable.AddDriver(typeof(FtdiSerialDriver));
        probeTable.AddDriver(typeof(ProlificSerialDriver));
        probeTable.AddDriver(typeof(Ch34xSerialDriver));
        return probeTable;
    }


    public List<IUsbSerialDriver> FindAllDrivers(UsbManager usbManager) => usbManager.DeviceList?.Values
        .Select(ProbeDevice).Where(driver => driver != null).ToList();

    public IUsbSerialDriver ProbeDevice(UsbDevice usbDevice)
    {
        var vendorId = usbDevice.VendorId;
        var productId = usbDevice.ProductId;

        var driverClass = _probeTable.FindDriver(vendorId, productId);
        if (driverClass == null)
        {
            return null;
        }

        IUsbSerialDriver driver;
        try
        {
            driver = (IUsbSerialDriver)Activator.CreateInstance(driverClass, new System.Object[] { usbDevice });
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

        return driver;
    }

}
