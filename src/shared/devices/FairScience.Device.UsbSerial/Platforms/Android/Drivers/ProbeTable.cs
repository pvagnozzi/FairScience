/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.Util;

namespace FairScience.Device.UsbSerial.Platforms.Android.Drivers;

public class ProbeTable
{
    private const string TAG = nameof(ProbeTable);

    private readonly Dictionary<Tuple<int, int>, Type> _probeTable = new();


    public ProbeTable AddProduct(int vendorId, int productId,
            Type driverClass)
    {
        var key = new Tuple<int, int>(vendorId, productId);

        if (!_probeTable.ContainsKey(key))
            _probeTable.Add(key, driverClass);

        return this;
    }

    public ProbeTable AddDriver(Type driverClass)
    {
        var m = driverClass.GetMethod("GetSupportedDevices");
        var devices =  (Dictionary<int, int[]>)m!.Invoke(null, null);

        foreach (var vendorId in devices!.Keys)
        {
            var productIds = devices[vendorId];

            foreach (var productId in productIds)
            {
                try
                {
                    AddProduct(vendorId, productId, driverClass);
                    Log.Debug(TAG, $"Added {vendorId:X}, {productId:X}, {driverClass}");
                }
                catch (Exception ex)
                {
                    Log.Debug(TAG, $"Error adding {vendorId:X}, {productId:X}, {driverClass}: {ex.Message}");
                    throw;
                }
            }
        }

        return this;
    }

    public Type FindDriver(int vendorId, int productId) => 
        _probeTable.TryGetValue(new Tuple<int, int>(vendorId, productId), out var result) ? result : null;
}
