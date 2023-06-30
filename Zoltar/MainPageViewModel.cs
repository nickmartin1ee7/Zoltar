using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;

using Rystem.OpenAi;
using Rystem.OpenAi.Completion;

namespace Zoltar;

public class MainPageViewModel : INotifyPropertyChanged
{

    private const string LAST_FORTUNE_KEY = "LAST_FORTUNE";
    private const string LAST_FORTUNE_USE_KEY = "LAST_FORTUNE_USE";

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
    }

    private async Task TrySetLastFortuneTextAsync()
    {
        try
        {
            FortuneText = await SecureStorage.GetAsync(LAST_FORTUNE_KEY);
            _logger.LogInformation("Loaded last fortune from storage successfully");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load last fortune from storage");
        }
    }

    private async Task<bool> CanReadFortuneAsync(bool autoUpdateWhenAllowed = false)
    {
        try
        {
            WaitTimeVisible = false;

            if (await _featureManager.IsEnabledAsync("zoltarunlimited"))
                return true;

            var lastFortune = await SecureStorage.GetAsync(LAST_FORTUNE_USE_KEY);

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

#if DEBUG
            return true;
#else
            return false;
#endif
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to read last fortune usage from storage");
            return true;
        }
    }

    private async Task GetFortuneAsync()
    {
        FortuneAllowed = false;
        IsLoading = true;

        _logger.LogInformation("User requested fortune");

        CompletionResult? result = null;

        try
        {
            result = await _ai.Completion.Request(_settings.OpenAi.Prompt)
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

        if (formattedResponse.Count(c => c == '"') == 2)
        {
            Index start = formattedResponse.IndexOf('"') + 1;
            Index end = formattedResponse.LastIndexOf('"');

            FortuneText = formattedResponse[start..end];
        }
        else
        {
            FortuneText = formattedResponse;
        }

        await SecureStorage.SetAsync(LAST_FORTUNE_USE_KEY, DateTime.Now.ToString());
        await SecureStorage.SetAsync(LAST_FORTUNE_KEY, FortuneText);

        _ = Task.Run(async () =>
            await TextToSpeech.SpeakAsync(FortuneText));

        FortuneAllowed = await CanReadFortuneAsync(autoUpdateWhenAllowed: true);
        IsLoading = false;

        _logger.LogInformation("User received fortune");

    }

    public async Task InitializeAsync()
    {
        FortuneAllowed = await CanReadFortuneAsync(autoUpdateWhenAllowed: true);

        if (_initialized)
            return;

        await TrySetLastFortuneTextAsync();

        _initialized = true;

        _logger.LogInformation("Application initialized");
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

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}