/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.App;
using Android.Content;
using Android.Hardware.Usb;

namespace FairScience.Device.Serial.Platforms.Android.Usb;

public static class UsbManagerExtensions
{
    
    public static Task<bool> RequestPermissionAsync(
        this UsbManager manager, 
        UsbDevice device,
        Context context,
        string permission,
        TaskCompletionSource<bool> completionSource = null)
    {
        completionSource ??= new TaskCompletionSource<bool>();

        var usbPermissionReceiver = new UsbPermissionReceiver(completionSource);
        context.RegisterReceiver(
            usbPermissionReceiver, 
            new IntentFilter(permission));
        var intent = PendingIntent.GetBroadcast(
            context, 
            0, 
            new Intent(permission), 
            0);
        manager.RequestPermission(device, intent);

        return completionSource.Task;
    }
}
