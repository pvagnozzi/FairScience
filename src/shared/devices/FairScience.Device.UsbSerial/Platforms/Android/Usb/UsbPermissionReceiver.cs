using Android.Content;
using Android.Hardware.Usb;

namespace FairScience.Device.Serial.Platforms.Android.Usb;

public class UsbPermissionReceiver : BroadcastReceiver
{
    private TaskCompletionSource<bool> CompletionSource { get; }

    public UsbPermissionReceiver(TaskCompletionSource<bool> completionSource)
    {
        CompletionSource = completionSource;
    }

    public override void OnReceive(Context context, Intent intent)
    {
#pragma warning disable CA1416
#pragma warning disable CA1422
        intent.GetParcelableExtra(UsbManager.ExtraDevice);
#pragma warning restore CA1422
#pragma warning restore CA1416
        var permissionGranted = intent.GetBooleanExtra(UsbManager.ExtraPermissionGranted, false);
        context.UnregisterReceiver(this);
        CompletionSource.TrySetResult(permissionGranted);
    }
}
