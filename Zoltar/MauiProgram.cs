using Microsoft.AppCenter.Distribute;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;

namespace Zoltar;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp(string userId = null)
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif
        var configProvider = new ConfigurationProvider();
        configProvider.Configure(builder.Configuration);

        var zoltarSettingsSnapshot = builder.Configuration
            .GetSection(nameof(ZoltarSettings))
            .Get<ZoltarSettings>();

        Distribute.SetEnabledAsync(true).GetAwaiter().GetResult();

        builder.Services
            .AddScoped<HttpClient>()
            .AddSingleton<ConfigurationProvider>(configProvider)
            .AddTransient<MainPageViewModel>()
            .AddTransient<MainPage>()
            .AddTransient<AppShell>()
            .AddLogging(loggingBuilder =>
            {
                if (zoltarSettingsSnapshot?.AppCenter?.Secret is null)
                    return;

                loggingBuilder.AddAppCenter(appCenterLoggerOptions =>
                    appCenterLoggerOptions.AppCenterAndroidSecret = zoltarSettingsSnapshot.AppCenter.Secret);
            })
            .AddFeatureManagement()
            ;

        return builder.Build();
    }
}
