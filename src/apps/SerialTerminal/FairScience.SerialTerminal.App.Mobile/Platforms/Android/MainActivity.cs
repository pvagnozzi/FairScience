using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Hardware.Usb;
using Android.OS;
using FairScience.Device.Serial;
// ReSharper disable once CheckNamespace
namespace FairScience.SerialTerminal.App.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
