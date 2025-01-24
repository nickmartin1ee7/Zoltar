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

    private ZoltarSettings Settings => configProvider
        .Configure()
        .GetSection(nameof(ZoltarSettings))
        .Get<ZoltarSettings>()!;

    private bool _initialized;
    private int _specialInteractions;
    private UserProfile? _userProfile;

    [ObservableProperty]
    private string? _fortuneHeaderText;

    [ObservableProperty]
    private string? _fortuneBodyText;

    [ObservableProperty]
    private string? _fortuneLuckText;

    [ObservableProperty]
    private string? _waitTimeText;

    [ObservableProperty]
    private bool _isWaitTimeVisible;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GetFortuneCommand))]
    private bool _isFortuneAllowed;

    [RelayCommand(CanExecute = nameof(IsFortuneAllowed))]
    private async Task GetFortune()
    {
        try
        {
            if (!IsFortuneAllowed
                || _userProfile is null)
            {
                return;
            }

            IsFortuneAllowed = false;
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
                SetFortuneText(
                    header: "...",
                    body: "Zoltar remains silent.",
                    luck: string.Empty);
                IsFortuneAllowed = true;
                return;
            }

            SetValuesFromApiResponse(result);
            await SaveLastFortune(result);

            if (_userProfile?.AnnounceFortune ?? false
                && !string.IsNullOrWhiteSpace(FortuneHeaderText)
                && !string.IsNullOrWhiteSpace(FortuneBodyText)
                && !string.IsNullOrWhiteSpace(FortuneLuckText))
            {
                var textToSaySb = new StringBuilder()
                    .AppendLine(FortuneHeaderText)
                    .AppendLine(" - ")
                    .AppendLine(FortuneBodyText)
                    .AppendLine(FortuneLuckText);
                _ = Task.Run(async () => await TextToSpeech.SpeakAsync(textToSaySb.ToString()));
            }

            IsFortuneAllowed = await CanReadFortuneAsync(autoUpdateWhenAllowed: true);
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
            var lastFortune = JsonSerializer.Deserialize<GenerateResponse>(lastFortuneJson
                ?? throw new ArgumentNullException(nameof(lastFortuneJson)));
            SetValuesFromApiResponse(lastFortune
                ?? throw new ArgumentNullException(nameof(lastFortune)));
            logger.LogInformation("Loaded last fortune from storage successfully");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to load last fortune from storage");
        }
    }

    private void SetValuesFromApiResponse(GenerateResponse fortuneResponse)
    {
        SetFortuneText(
            header: fortuneResponse?.fortune?.header ?? string.Empty,
            body: fortuneResponse?.fortune?.body ?? string.Empty,
            luck: !string.IsNullOrWhiteSpace(fortuneResponse?.luckText)
                ? $"Your luck today is {fortuneResponse.luckText.Trim()}"
                : string.Empty);
    }

    private async Task<bool> CanReadFortuneAsync(bool autoUpdateWhenAllowed = false, bool skipWait = false)
    {
        _specialInteractions = 0;

        try
        {
            IsWaitTimeVisible = false;

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
            IsWaitTimeVisible = true;

            if (autoUpdateWhenAllowed)
            {
                _ = Task.Run(async () =>
                {
                    var untilNextRun = next.Subtract(DateTimeOffset.Now);
                    await Task.Delay(untilNextRun);
                    IsFortuneAllowed = await CanReadFortuneAsync();
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

        await TryAskForNotificationsPermission();

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

    private static async Task TryAskForNotificationsPermission()
    {
        if (await Permissions.CheckStatusAsync<Permissions.PostNotifications>() == PermissionStatus.Granted)
        {
            return;
        }

        await Permissions.RequestAsync<Permissions.PostNotifications>();
    }

    private static async Task SaveLastFortune(GenerateResponse result)
    {
        // Set last fortune
        var resultJson = JsonSerializer.Serialize(result);
        await SecureStorage.SetAsync(Constants.LAST_FORTUNE_USE_KEY, DateTimeOffset.Now.ToString());
        await SecureStorage.SetAsync(Constants.LAST_FORTUNE_KEY, resultJson);

        // Update collection of previous fortunes
        var previousFortunes = new List<TimestampedGenerateReponse>();
        var previousFortunesJson = await SecureStorage.GetAsync(Constants.PREVIOUS_FORTUNES_KEY);
        if (previousFortunesJson is not null)
        {
            previousFortunes = JsonSerializer.Deserialize<List<TimestampedGenerateReponse>>(previousFortunesJson)
                ?? throw new ArgumentNullException(nameof(previousFortunesJson));
        }
        previousFortunes.Add(new(result, DateTime.Now));
        await SecureStorage.SetAsync(Constants.PREVIOUS_FORTUNES_KEY, JsonSerializer.Serialize(previousFortunes));
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

        IsFortuneAllowed = await CanReadFortuneAsync(autoUpdateWhenAllowed: true);

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

    private void SetFortuneText(string header, string body, string luck)
    {
        FortuneHeaderText = header.Replace("\n", string.Empty).Trim();
        FortuneBodyText = body.Replace("\n", string.Empty).Trim();
        FortuneLuckText = luck.Replace("\n", string.Empty).Trim();
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
        IsFortuneAllowed = await CanReadFortuneAsync(skipWait: true);
        WaitTimeText = "Zoltar grants you another fortune.";
    }
}