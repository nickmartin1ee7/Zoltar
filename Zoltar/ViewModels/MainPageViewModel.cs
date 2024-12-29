using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.AppCenter.Distribute;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;

using Zoltar.Models;
using Zoltar.Models.Services;

namespace Zoltar;

public partial class MainPageViewModel(
    HttpClient httpClient,
    ILogger<MainPageViewModel> logger,
    CustomConfigurationProvider configProvider,
    IFeatureManager featureManager,
    IAlarmScheduler alarmScheduler)
    : ObservableObject
{
    private const int MAX_SPECIAL_INTERACTIONS = 5;
    private const string DEFAULT_FORTUNE_HEADER = "Zoltar, The Fortune Teller";

    private ZoltarSettings Settings => configProvider
        .Configure()
        .GetSection(nameof(ZoltarSettings))
        .Get<ZoltarSettings>()!;

    private bool _initialized;
    private int _specialInteractions;
    private UserProfile? _userProfile;

    [ObservableProperty]
    private string _fortuneHeader = DEFAULT_FORTUNE_HEADER;

    [ObservableProperty]
    private string? _fortuneText;

    [ObservableProperty]
    private string? _waitTimeText;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _waitTimeVisible;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GetFortuneCommand))]
    private bool _fortuneAllowed;

    [RelayCommand(CanExecute = nameof(FortuneAllowed))]
    private async Task GetFortune()
    {
        try
        {
            if (!FortuneAllowed
                || _userProfile is null)
            {
                return;
            }

            FortuneAllowed = false;
            IsLoading = true;

            logger.LogInformation("User requested fortune");

            GenerateResponse? result = null;
            HttpResponseMessage? response = null;

            try
            {
                var (Context, Luck) = BuildPrompt(_userProfile);
                var requestContent = JsonContent.Create(Context);
                requestContent.Headers.Add("X-API-KEY", Settings?.Api?.ApiKey
                    ?? throw new ArgumentNullException(nameof(Settings.Api.ApiKey)));

                response = await httpClient.PostAsync($"{Settings.Api.Url}/generate", requestContent);
                result = await response.Content.ReadFromJsonAsync<GenerateResponse>()
                    ?? throw new ArgumentNullException(nameof(result));
                result.luckText ??= Luck;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to communicate API. Response code: {responseCode}", response?.StatusCode);
            }

            if (result?.fortune is null)
            {
                logger.LogWarning("User saw no fortune");

                FortuneHeader = DEFAULT_FORTUNE_HEADER;
                FortuneText = "Zoltar remains silent.";
                FortuneAllowed = true;
                return;
            }

            SetValuesFromApiResponse(result);

            await SaveLastFortune(result);

            if (_userProfile?.AnnounceFortune ?? false
                && !string.IsNullOrWhiteSpace(FortuneText))
            {
                _ = Task.Run(async () => await TextToSpeech.SpeakAsync(FortuneText!));
            }

            FortuneAllowed = await CanReadFortuneAsync(autoUpdateWhenAllowed: true);

            logger.LogInformation("User received fortune");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task OnboardUser()
    {
        logger.LogInformation("Onboarding user");
        await Shell.Current.GoToAsync($"///{nameof(OnboardingPage)}");
    }

    [RelayCommand]
    private async Task ShowPreviousFortunes()
    {
        await Shell.Current.GoToAsync($"///{nameof(PreviousFortunesPage)}");
    }

    private async Task TrySetLastFortuneTextAsync()
    {
        try
        {
            var lastFortuneJson = await SecureStorage.GetAsync(Constants.LAST_FORTUNE_KEY);
            var lastFortune = JsonSerializer.Deserialize<GenerateResponse>(lastFortuneJson ?? throw new ArgumentNullException(nameof(lastFortuneJson)));
            SetValuesFromApiResponse(lastFortune);
            logger.LogInformation("Loaded last fortune from storage successfully");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to load last fortune from storage");
        }
    }

    private void SetValuesFromApiResponse(GenerateResponse fortuneResponse)
    {
        if (fortuneResponse?.fortune is not null)
        {
            var fortune = $"{fortuneResponse.fortune.header} - {fortuneResponse.fortune.body}";
            var formattedResponse = FormatFortuneText(fortune);
            FortuneText = formattedResponse;
        }

        if (!string.IsNullOrWhiteSpace(fortuneResponse?.luckText))
        {
            FortuneHeader = $"Your luck today is {fortuneResponse.luckText}";
        }
    }

    private async Task<bool> CanReadFortuneAsync(bool autoUpdateWhenAllowed = false, bool skipWait = false)
    {
        _specialInteractions = 0;

        try
        {
            WaitTimeVisible = false;

            if (await featureManager.IsEnabledAsync(Constants.FEATURE_ZOLTAR_UNLIMITED))
            {
                return true;
            }

            var lastFortune = await SecureStorage.GetAsync(Constants.LAST_FORTUNE_USE_KEY);

            if (string.IsNullOrEmpty(lastFortune))
            {
                return true;
            }

            var lastFortuneTime = DateTimeOffset.Parse(lastFortune);

#if DEBUG
            var next = new DateTimeOffset(lastFortuneTime.AddSeconds(30).DateTime, lastFortuneTime.Offset);
#else
            var next = new DateTimeOffset(lastFortuneTime.AddDays(1).Date, lastFortuneTime.Offset);
#endif

            if (DateTimeOffset.Now > next)
            {
                return true;
            }

            WaitTimeText = $"Your fate changes at {next:MMM d h:mm:ss tt}";
            WaitTimeVisible = true;

            if (autoUpdateWhenAllowed)
            {
                _ = Task.Run(async () =>
                {
                    var untilNextRun = next.Subtract(DateTimeOffset.Now);
                    await Task.Delay(untilNextRun);
                    FortuneAllowed = await CanReadFortuneAsync();
                });

                var unixTicksInMs = next.ToUnixTimeMilliseconds();
                alarmScheduler.ScheduleNotification(unixTicksInMs);
            }

            if (skipWait)
            {
                return true;
            }

            return false;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to read last fortune usage from storage");
            return true;
        }
    }

    private async Task CheckForNotificationsPermissionAsync()
    {
#if ANDROID21_0_OR_GREATER
        var shouldPrompt = true;

        try
        {
            if (!bool.TryParse(await SecureStorage.GetAsync(Constants.PROMPT_NOTIFICATIONS_KEY), out shouldPrompt))
            {
                await SetPromptNotificationsAsync(shouldPrompt = true); // Default to prompt user
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unable to read if user had been prompted for notifications before");
        }

        if (shouldPrompt && !AreDeviceNotificationsEnabled())
        {
            var userPermissionResult = await Application.Current!.MainPage!.DisplayAlert(
                "Enable Notifications",
                "To be notified when a New Fortune is ready, please enable the notification permission for this app.",
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
                logger.LogError(e, "Unable to set the user's preference for notifications");
            }
        }
#endif
    }

    private static async Task SetPromptNotificationsAsync(bool shouldPrompt)
    {
        await SecureStorage.SetAsync(Constants.PROMPT_NOTIFICATIONS_KEY, shouldPrompt.ToString());
    }

    private static bool AreDeviceNotificationsEnabled() =>
        AndroidX.Core.App.NotificationManagerCompat.From(Platform.CurrentActivity!).AreNotificationsEnabled();

    private static async Task SaveLastFortune(GenerateResponse result)
    {
        // Set last fortune
        var resultJson = JsonSerializer.Serialize(result);
        await SecureStorage.SetAsync(Constants.LAST_FORTUNE_USE_KEY, DateTimeOffset.Now.ToString());
        await SecureStorage.SetAsync(Constants.LAST_FORTUNE_KEY, resultJson);

        // Update collection of previous fortunes
        var previousFortunes = new List<TimestampedGenerateReponse>();
        var previousFortunesRaw = await SecureStorage.GetAsync(Constants.PREVIOUS_FORTUNES_KEY);
        if (previousFortunesRaw is not null)
        {
            previousFortunes = JsonSerializer.Deserialize<List<TimestampedGenerateReponse>>(previousFortunesRaw);
        }
        previousFortunes.Add(new(result, DateTime.Now));
        await SecureStorage.SetAsync(Constants.PREVIOUS_FORTUNES_KEY, JsonSerializer.Serialize(previousFortunes));
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
                      $"You know the stranger is named {userProfile.Name}, ");

        if (userProfile.Birthday.HasValue)
        {
            contextSb.Append($"their birthday is {userProfile.Birthday:d}, ");
        }

        if (userProfile.UseAstrology)
        {
            contextSb.Append($"their astrological sign is {userProfile.Sign} (mention their sign), ");
        }

        // Final line
        contextSb.AppendLine($"and their fortune today is {luck}.");

        return (contextSb.ToString().Trim(), luck);
    }

    public async Task InitializeAsync()
    {
        await CheckForNotificationsPermissionAsync();
        await TryOnboardNewUserAsync();

        FortuneAllowed = await CanReadFortuneAsync(autoUpdateWhenAllowed: true);

        if (_initialized)
        {
            return;
        }

        Distribute.CheckForUpdate();

        await TrySetLastFortuneTextAsync();

        _initialized = true;

        logger.LogInformation("Application initialized");
    }

    public async Task TryOnboardNewUserAsync()
    {
        try
        {
            var userProfileJson = await SecureStorage.GetAsync(Constants.USER_PROFILE_KEY);

            if (string.IsNullOrEmpty(userProfileJson))
            {
                await OnboardUser();
                userProfileJson = await SecureStorage.GetAsync(Constants.USER_PROFILE_KEY);
            }

            _userProfile = JsonSerializer.Deserialize<UserProfile>(userProfileJson ?? throw new ArgumentNullException(nameof(userProfileJson)));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to load user profile from storage");
        }
    }

    public async Task InvokeSpecialInteractionAsync()
    {
        if (!(await featureManager.IsEnabledAsync(Constants.FEATURE_ZOLTAR_SECRET_INTERACTION)))
        {
            return;
        }

        _specialInteractions++;

        if (_specialInteractions < MAX_SPECIAL_INTERACTIONS)
        {
            return;
        }

        _specialInteractions = 0;
        FortuneAllowed = await CanReadFortuneAsync(skipWait: true);
        WaitTimeText = "Zoltar grants you another fortune.";
    }
}