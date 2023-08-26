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

    private ZoltarSettings _settings => _configProvider
        .Configure()
        .GetSection(nameof(ZoltarSettings))
        .Get<ZoltarSettings>();

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
        IFeatureManager featureManager)
    {
        _configProvider = configProvider;
        _featureManager = featureManager;
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
            FortuneText = await SecureStorage.GetAsync(Constants.LAST_FORTUNE_KEY);
            _logger.LogInformation("Loaded last fortune from storage successfully");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load last fortune from storage");
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

            var lastFortuneTime = DateTime.Parse(lastFortune);
            var next = lastFortuneTime.AddDays(1).Date;

            if (DateTime.Now > next)
                return true;

            WaitTimeText = $"Your fate changes at {next}";
            WaitTimeVisible = true;

            if (autoUpdateWhenAllowed)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(next - DateTime.Now);
                    FortuneAllowed = await CanReadFortuneAsync();
                });
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

    private async Task GetFortuneAsync()
    {
        if (!FortuneAllowed)
            return;

        FortuneAllowed = false;
        IsLoading = true;

        _logger.LogInformation("User requested fortune");

        string result = null;
        HttpResponseMessage response = null;

        try
        {
            var requestContent = JsonContent.Create(BuildPrompt(_userProfile));
            requestContent.Headers.Add("X-API-KEY", _settings.Api.ApiKey);

            response = await _client.PostAsync($"{_settings.Api.Url}/generate", requestContent);
            result = (await response.Content.ReadFromJsonAsync<FortuneApiResponse>())?.Fortune;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to communicate with OpenAi. Response code: {responseCode}", response?.StatusCode);
        }

        if (result is null)
        {
            _logger.LogWarning("User saw no fortune");

            FortuneText = "Zoltar remains silent.";
            FortuneAllowed = true;
            IsLoading = false;
            return;
        }


        var formattedResponse = result
            .Replace("\"", string.Empty)
            .TrimStart()
            .TrimEnd();

        FortuneText = formattedResponse;

        await SecureStorage.SetAsync(Constants.LAST_FORTUNE_USE_KEY, DateTime.Now.ToString());
        await SecureStorage.SetAsync(Constants.LAST_FORTUNE_KEY, FortuneText);

        _ = Task.Run(async () =>
            await TextToSpeech.SpeakAsync(FortuneText));

        FortuneAllowed = await CanReadFortuneAsync(autoUpdateWhenAllowed: true);
        IsLoading = false;

        _logger.LogInformation("User received fortune");
    }

    private string BuildPrompt(UserProfile userProfile)
    {
        var sb = new StringBuilder();
        var luck = userProfile.Luck;

        sb.Append($"The today is {DateTime.Now.ToShortDateString()}. " +
                      $"You know the stranger is named {userProfile.Name}, " +
                      $"their birthday is {userProfile.Birthday:d}, ");

        if (userProfile.UseAstrology)
        {
            sb.Append($"their astrological sign is {userProfile.Sign}, ");
        }

        sb.AppendLine($"and their fortune today is {luck}.");

        return sb.ToString();
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