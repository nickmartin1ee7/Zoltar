using System.Reflection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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

        SetupConfiguration(builder);

        var zoltarSettings = builder.Configuration
            .GetSection(nameof(ZoltarSettings))
            .Get<ZoltarSettings>() ?? throw new ArgumentNullException(nameof(ZoltarSettings));

        builder.Services
            .AddOpenAi(settings =>
            {
                settings.ApiKey = zoltarSettings.OpenAi.Key;
            })
            .AddSingleton<ZoltarSettings>(zoltarSettings)
            .AddTransient<MainPageViewModel>()
            .AddTransient<MainPage>()
            .AddTransient<AppShell>()
            .AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddAppCenter(appCenterLoggerOptions =>
                    appCenterLoggerOptions.AppCenterAndroidSecret = zoltarSettings.AppCenter.Secret);
            })
            ;

        return builder.Build();
    }

    private static void SetupConfiguration(MauiAppBuilder builder)
    {
        var assembly = Assembly.GetExecutingAssembly();

        var configFileName = $"{assembly.GetName().Name}.Resources.appsettings.json";

        using var configStream = assembly
                                     .GetManifestResourceStream(configFileName)
                                 ?? throw new ArgumentException($"Configuration file ({configFileName}) not found",
                                     nameof(configFileName));

        var tempConfig = new ConfigurationBuilder()
            .AddJsonStream(configStream)
            .Build();

        var aacConnStr = tempConfig.GetConnectionString("aac-zoltar");

        builder.Configuration
            .AddAzureAppConfiguration(aacConnStr);
    }
}
