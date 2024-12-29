namespace Zoltar.Models;

public class ZoltarSettings
{
    public AppCenterSettings? AppCenter { get; set; }

    public ApiSettings? Api { get; set; }
    public TelemetrySettings? Telemetry { get; set; }

    public class AppCenterSettings
    {
        public string? Secret { get; set; }
    }

    public class ApiSettings
    {
        public string? Url { get; set; }
        public string? ApiKey { get; set; }
    }

    public class TelemetrySettings
    {
        public string? Url { get; set; }
        public string? Key { get; set; }
    }
}
