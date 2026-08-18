using Microsoft.Extensions.Configuration;

using Xunit;

using Zoltar.Models;

namespace Zoltar.Tests;

public class ConfigurationTests
{
    /// <summary>
    /// Verifies the appsettings.json contract still binds to <see cref="ZoltarSettings"/>
    /// after the config package upgrade. This exercises the local configuration path that
    /// <see cref="Zoltar.Models.Services.CustomConfigurationProvider"/> falls back to when the
    /// Azure App Configuration connection string is empty or unreachable. (A live Azure App
    /// Configuration endpoint cannot be exercised in CI without a connection string.)
    /// </summary>
    [Fact]
    public void ZoltarSettings_BindsFromAppSettingsJson()
    {
        var basePath = AppContext.BaseDirectory;
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var settings = config.GetSection(nameof(ZoltarSettings)).Get<ZoltarSettings>();

        Assert.NotNull(settings);
        Assert.NotNull(settings!.Api);
        Assert.Equal(string.Empty, settings.Api!.ApiKey);
        Assert.Equal(string.Empty, settings.Api!.Url);
        Assert.Null(settings.Telemetry); // Not present in base appsettings.json
    }
}