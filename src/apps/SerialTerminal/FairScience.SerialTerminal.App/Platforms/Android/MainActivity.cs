using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Hardware.Usb;
using Android.OS;
using Android.Widget;
using FairScience.Device.Serial;

// ReSharper disable once CheckNamespace
namespace FairScience.SerialTerminal.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var usbManager = (UsbManager)GetSystemService (UsbService);
        var serialPortProvider = new SerialPortProvider(usbManager);
        var portName = serialPortProvider.GetPortNames();
        Console.Write(portName.FirstOrDefault());
    }
}
