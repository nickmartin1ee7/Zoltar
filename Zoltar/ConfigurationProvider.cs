using System.Reflection;

using Microsoft.Extensions.Configuration;

namespace Zoltar
{
    public class ConfigurationProvider
    {
        private bool _hadError = false;
        private IConfiguration _config;

        public IConfiguration Configure(IConfigurationBuilder builder = null)
        {
            if (!_hadError && _config is not null)
                return _config;

            var assembly = Assembly.GetExecutingAssembly();

            var configFileName = $"{assembly.GetName().Name}.Resources.appsettings.json";

            using var configStream = assembly
                                         .GetManifestResourceStream(configFileName)
                                     ?? throw new ArgumentException($"Configuration file ({configFileName}) not found",
                                         nameof(configFileName));

            var tempConfig = new ConfigurationBuilder()
                .AddJsonStream(configStream)
                .Build();

            var configBuilder = builder ?? new ConfigurationManager();

            try
            {
                var aacConnStr = tempConfig.GetConnectionString("aac");

                _config = configBuilder
                    .AddConfiguration(tempConfig)
                    .AddAzureAppConfiguration(options =>
                    {
                        options.ConfigureClientOptions(configure =>
                        {
                            configure.Retry.NetworkTimeout = TimeSpan.FromSeconds(5);
                            configure.Retry.MaxRetries = 2;
                        });

                        options
                            .Connect(aacConnStr)
                            .UseFeatureFlags();
                    })
                    .Build();
                _hadError = false;
            }
            catch (Exception ex)
            {
                _config = configBuilder
                    .AddConfiguration(tempConfig)
                    .Build();
                _hadError = true;
            }

            return _config;
        }
    }
}
