using System.ComponentModel;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows.Input;

using Microsoft.AppCenter.Distribute;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;

namespace Zoltar;

public class MainPageViewModel : INotifyPropertyChanged
{
    private const int MAX_SPECIAL_INTERACTIONS = 5;

    private readonly ILogger<MainPageViewModel> _logger;
    private readonly HttpClient _client;
    private readonly ConfigurationProvider _configProvider;
    private readonly IFeatureManager _featureManager;
    private readonly IAlarmScheduler _alarmScheduler;

    private ZoltarSettings _settings => _configProvider
        .Configure()
        .GetSection(nameof(ZoltarSettings))
        .Get<ZoltarSettings>();

    private string _fortuneHeader = "Zoltar, The Fortune Teller";
    private string _fortuneText;
    private string _waitTimeText;
    private bool _fortuneAllowed;
    private bool _isLoading;
    private bool _waitTimeVisible;
    private bool _initialized;
    private int _specialInteractions;
    private UserProfile _userProfile;

    public MainPageViewModel(HttpClient httpClient,
        ILogger<MainPageViewModel> logger,
        ConfigurationProvider configProvider,
        IFeatureManager featureManager,
        IAlarmScheduler alarmScheduler)
    {
        _configProvider = configProvider;
        _featureManager = featureManager;
        _alarmScheduler = alarmScheduler;
        _logger = logger;
        _client = httpClient;

        FortuneCommand = new Command(
            execute: async () => await GetFortuneAsync(),
            canExecute: () => FortuneAllowed);

        OnboardCommand = new Command(
            execute: async () => await OnboardUserAsync());
    }

    public Command OnboardCommand { get; set; }

    private async Task OnboardUserAsync()
    {
        _logger.LogInformation("Onboarding user");
        await Shell.Current.GoToAsync($"///{nameof(OnboardingPage)}");
    }

    private async Task TrySetLastFortuneTextAsync()
    {
        try
        {
            var lastFortune = JsonSerializer.Deserialize<FortuneApiResponse>(await SecureStorage.GetAsync(Constants.LAST_FORTUNE_KEY));
            SetValuesFromApiResponse(lastFortune);
            _logger.LogInformation("Loaded last fortune from storage successfully");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load last fortune from storage");
        }
    }

    private void SetValuesFromApiResponse(FortuneApiResponse fortuneResponse)
    {
        if (!string.IsNullOrWhiteSpace(fortuneResponse.Fortune))
        {
            var formattedResponse = FormatFortuneText(fortuneResponse.Fortune);
            FortuneText = formattedResponse;
        }

        if (!string.IsNullOrWhiteSpace(fortuneResponse.LuckText))
        {
            FortuneHeader = fortuneResponse.LuckText;
        }
    }

    private async Task<bool> CanReadFortuneAsync(bool autoUpdateWhenAllowed = false, bool skipWait = false)
    {
        _specialInteractions = 0;

        try
        {
            WaitTimeVisible = false;

            if (await _featureManager.IsEnabledAsync(Constants.FEATURE_ZOLTAR_UNLIMITED))
                return true;

            var lastFortune = await SecureStorage.GetAsync(Constants.LAST_FORTUNE_USE_KEY);

            if (string.IsNullOrEmpty(lastFortune))
                return true;

            var lastFortuneTime = DateTimeOffset.Parse(lastFortune);

#if DEBUG
            var next = new DateTimeOffset(lastFortuneTime.AddSeconds(30).DateTime, lastFortuneTime.Offset);
#else
            var next = new DateTimeOffset(lastFortuneTime.AddDays(1).Date, lastFortuneTime.Offset);
#endif

            if (DateTimeOffset.Now > next)
                return true;

            WaitTimeText = $"Your fate changes at {next:MMM d h:mm:ss tt}";
            WaitTimeVisible = true;

            if (autoUpdateWhenAllowed)
            {
                await CheckForNotificationsPermissionAsync();

                _ = Task.Run(async () =>
                {
                    var untilNextRun = next.Subtract(DateTimeOffset.Now);
                    await Task.Delay(untilNextRun);
                    FortuneAllowed = await CanReadFortuneAsync();
                });

                var unixTicksInMs = next.ToUnixTimeMilliseconds();
                _alarmScheduler.ScheduleNotification(unixTicksInMs);
            }

            if (skipWait)
                return true;

            return false;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to read last fortune usage from storage");
            return true;
        }
    }

    private async Task CheckForNotificationsPermissionAsync()
    {
#if ANDROID21_0_OR_GREATER
        var shouldPrompt = true;

        try
        {
            if (!bool.TryParse(await SecureStorage.GetAsync("prompt_notifications"), out shouldPrompt))
            {
                await SetPromptNotificationsAsync(shouldPrompt = true); // Default to prompt user
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to read if user had been prompted for notifications before");
        }

        if (shouldPrompt && !AreDeviceNotificationsEnabled())
        {
            var userPermissionResult = await Application.Current!.MainPage!.DisplayAlert(
                "Enable Notifications",
                "Your notifications to receive New Fortune updates are currently turned off. To receive notifications, you need to enable this permission.",
                "Go to Settings",
                "Cancel");

            if (userPermissionResult)
            {
                AppInfo.ShowSettingsUI();
            }

            try
            {
                await SetPromptNotificationsAsync(userPermissionResult); // Don't prompt again
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to set the user's preference for notifications");
            }
        }
#endif
    }

    private static async Task SetPromptNotificationsAsync(bool shouldPrompt)
    {
        await SecureStorage.SetAsync("prompt_notifications", shouldPrompt.ToString());
    }

    private static bool AreDeviceNotificationsEnabled() =>
        AndroidX.Core.App.NotificationManagerCompat.From(Platform.CurrentActivity!).AreNotificationsEnabled();



    private async Task GetFortuneAsync()
    {
        try
        {
            if (!FortuneAllowed)
                return;

            FortuneAllowed = false;
            IsLoading = true;

            _logger.LogInformation("User requested fortune");

            FortuneApiResponse result = null;
            HttpResponseMessage response = null;

            try
            {
                var (Context, Luck) = BuildPrompt(_userProfile);
                var requestContent = JsonContent.Create(Context);
                requestContent.Headers.Add("X-API-KEY", _settings.Api.ApiKey);

                response = await _client.PostAsync($"{_settings.Api.Url}/generate", requestContent);
                result = await response.Content.ReadFromJsonAsync<FortuneApiResponse>();
                result.LuckText ??= Luck;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to communicate API. Response code: {responseCode}", response?.StatusCode);
            }

            if (result?.Fortune is null)
            {
                _logger.LogWarning("User saw no fortune");

                FortuneHeader = "Zoltar, The Fortune Teller";
                FortuneText = "Zoltar remains silent.";
                FortuneAllowed = true;
                return;
            }

            SetValuesFromApiResponse(result);

            await SecureStorage.SetAsync(Constants.LAST_FORTUNE_USE_KEY, DateTimeOffset.Now.ToString());
            await SecureStorage.SetAsync(Constants.LAST_FORTUNE_KEY, JsonSerializer.Serialize(result));

            _ = Task.Run(async () =>
                await TextToSpeech.SpeakAsync(FortuneText));

            FortuneAllowed = await CanReadFortuneAsync(autoUpdateWhenAllowed: true);

            _logger.LogInformation("User received fortune");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string FormatFortuneText(string fortuneText)
    {
        return fortuneText
            .Replace("\"", string.Empty)
            .TrimStart()
            .TrimEnd();
    }

    private static (string Context, string Luck) BuildPrompt(UserProfile userProfile)
    {
        var contextSb = new StringBuilder();
        var luck = userProfile.Luck;

        contextSb.Append($"The today is {DateTime.Now.ToShortDateString()}. " +
                      $"You know the stranger is named {userProfile.Name}, " +
                      $"their birthday is {userProfile.Birthday:d}, ");

        if (userProfile.UseAstrology)
        {
            contextSb.Append($"their astrological sign is {userProfile.Sign}, ");
        }

        contextSb.AppendLine($"and their fortune today is {luck}.");

        return (contextSb.ToString(), luck);
    }

    public async Task InitializeAsync()
    {
        await TryOnboardNewUserAsync();
        FortuneAllowed = await CanReadFortuneAsync(autoUpdateWhenAllowed: true);

        if (_initialized)
            return;

        Distribute.CheckForUpdate();

        await TrySetLastFortuneTextAsync();

        _initialized = true;

        _logger.LogInformation("Application initialized");
    }

    public async Task TryOnboardNewUserAsync()
    {
        try
        {
            var userProfileJson = await SecureStorage.GetAsync(Constants.USER_PROFILE_KEY);

            if (string.IsNullOrEmpty(userProfileJson))
            {
                await OnboardUserAsync();
                userProfileJson = await SecureStorage.GetAsync(Constants.USER_PROFILE_KEY);
            }

            _userProfile = JsonSerializer.Deserialize<UserProfile>(userProfileJson);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to load user profile from storage");
        }
    }

    public async Task InvokeSpecialInteractionAsync()
    {
        if (!(await _featureManager.IsEnabledAsync(Constants.FEATURE_ZOLTAR_SECRET_INTERACTION)))
            return;

        _specialInteractions++;

        if (_specialInteractions < MAX_SPECIAL_INTERACTIONS)
            return;

        _specialInteractions = 0;
        FortuneAllowed = await CanReadFortuneAsync(skipWait: true);
        WaitTimeText = "Zoltar grants you another fortune.";
    }

    public string FortuneHeader
    {
        get => _fortuneHeader;
        set
        {
            if (value == _fortuneHeader) return;
            _fortuneHeader = value;
            OnPropertyChanged();
        }
    }

    public string FortuneText
    {
        get => _fortuneText;
        set
        {
            if (value == _fortuneText) return;
            _fortuneText = value;
            OnPropertyChanged();
        }
    }

    public bool FortuneAllowed
    {
        get => _fortuneAllowed;
        set
        {
            if (value == _fortuneAllowed) return;
            _fortuneAllowed = value;
            OnPropertyChanged();
            ((Command)FortuneCommand).ChangeCanExecute();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (value == _isLoading) return;
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public bool WaitTimeVisible
    {
        get => _waitTimeVisible;
        set
        {
            if (value == _waitTimeVisible) return;
            _waitTimeVisible = value;
            OnPropertyChanged();
        }
    }

    public string WaitTimeText
    {
        get => _waitTimeText;
        set
        {
            if (value == _waitTimeText) return;
            _waitTimeText = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public ICommand FortuneCommand { get; }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}