using Microsoft.AppCenter.Distribute;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;
using Microsoft.Maui.Controls.Compatibility.Platform.Android;

using Serilog;

using Zoltar.Models;
using Zoltar.Models.Services;

namespace Zoltar;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp(string? userId = null)
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            Log.CloseAndFlush();
        };

        Distribute.UpdateTrack = UpdateTrack.Public;
        Distribute.SetEnabledAsync(true).GetAwaiter().GetResult();
        Distribute.CheckForUpdate();

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        var configProvider = new CustomConfigurationProvider();

        var zoltarSettingsSnapshot = configProvider
            .Configure(builder.Configuration)
            .GetSection(nameof(ZoltarSettings))
            .Get<ZoltarSettings>();

        builder.Services
            .AddScoped<HttpClient>()
            .AddSingleton<CustomConfigurationProvider>(configProvider)
            .AddTransient<IAlarmScheduler, AlarmScheduler>()
            .AddTransient<PreviousFortunesViewModel>()
            .AddTransient<PreviousFortunesPage>()
            .AddTransient<OnboardingPage>()
            .AddTransient<MainPageViewModel>()
            .AddTransient<MainPage>()
            .AddTransient<AppShell>()
            .AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddSerilog(Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Verbose()
                    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .Enrich.WithMachineName()
                    .Enrich.WithThreadId()
                    .Enrich.WithThreadName()
                    .Enrich.WithProperty(nameof(userId), userId)
                    .Enrich.WithProperty("version", AppInfo.Current.VersionString)
                    .WriteTo.Console()
                    .WriteTo.Seq(
                        serverUrl: zoltarSettingsSnapshot?.Telemetry?.Url ?? string.Empty,
                        apiKey: zoltarSettingsSnapshot?.Telemetry?.Key ?? string.Empty)
                    .CreateLogger());

                if (zoltarSettingsSnapshot?.AppCenter?.Secret is null)
                    return;

                loggingBuilder
                    .AddAppCenter(appCenterLoggerOptions =>
                        appCenterLoggerOptions.AppCenterAndroidSecret = zoltarSettingsSnapshot.AppCenter.Secret);
            })
            .AddFeatureManagement();

        SetCustomHandlers();

        return builder.Build();
    }

    private static void SetCustomHandlers()
    {
        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("NoUnderline", (h, v) =>
        {
            // Set underline color to transparent for Android
            h.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Colors.Transparent.ToAndroid());
        });
    }
}
