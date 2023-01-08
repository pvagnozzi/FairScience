using Android.App;
using Android.Runtime;

// ReSharper disable once CheckNamespace
namespace FairScience.SerialTerminal.App;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(nint handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
