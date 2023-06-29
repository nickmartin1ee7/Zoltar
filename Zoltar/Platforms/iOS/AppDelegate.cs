using Foundation;

using UIKit;

namespace Zoltar;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp(
        UIDevice.CurrentDevice.IdentifierForVendor.AsString());
}
