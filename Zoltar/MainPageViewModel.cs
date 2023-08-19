using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows.Input;

using Microsoft.AppCenter.Distribute;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;

using Rystem.OpenAi;
using Rystem.OpenAi.Completion;

namespace Zoltar;

public class MainPageViewModel : INotifyPropertyChanged
{
    private const int MAX_SPECIAL_INTERACTIONS = 5;

    private readonly ILogger<MainPageViewModel> _logger;
    private readonly IOpenAi _ai;
    private readonly ZoltarSettings _settings;
    private readonly IFeatureManager _featureManager;

    private string _fortuneText;
    private string _waitTimeText;
    private bool _fortuneAllowed;
    private bool _isLoading;
    private bool _waitTimeVisible;
    private bool _initialized;
    private int _specialInteractions;
    private UserProfile _userProfile;

    public MainPageViewModel(IOpenAiFactory openAiFactory,
        ILogger<MainPageViewModel> logger,
        ZoltarSettings settings,
        IFeatureManager featureManager)
    {
        _settings = settings;
        _featureManager = featureManager;
        _logger = logger;
        _ai = openAiFactory.Create();

        FortuneCommand = new Command(
            execute: async () => await GetFortuneAsync(),
            canExecute: () => FortuneAllowed);

        OnboardCommand = new Command(
            execute: async () => await OnboardUserAsync());
    }

    public Command OnboardCommand { get; set; }

    private async Task OnboardUserAsync()
    {
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

        CompletionResult result = null;

        try
        {
            result = await _ai.Completion.Request(BuildPrompt(_userProfile, _settings.OpenAi.Prompt))
                .WithModel(TextModelType.DavinciText3)
                .SetMaxTokens(_settings.OpenAi.MaxTokens)
                .ExecuteAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to communicate with OpenAi");
        }

        if (result?.Completions is null)
        {
            _logger.LogWarning("User saw no fortune");

            FortuneText = "Zoltar remains silent.";
            FortuneAllowed = true;
            IsLoading = false;
            return;
        }


        var formattedResponse = string.Join(Environment.NewLine,
                result.Completions.Select(c => c.Text))
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

    private string BuildPrompt(UserProfile userProfile, string prompt)
    {
        var sb = new StringBuilder();
        var luck = userProfile.Luck;

        sb.AppendLine(prompt);

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