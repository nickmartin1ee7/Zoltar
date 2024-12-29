using System.Text.Json;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Zoltar.Models;

namespace Zoltar;

public partial class OnboardingViewModel : ObservableObject
{
    private Func<string, string, string, Task>? _alertFunc;

    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBirthday))]
    private string? _birthday;

    public bool HasBirthday =>
        !string.IsNullOrEmpty(Birthday)
        && DateTime.TryParse(Birthday, out _);

    [ObservableProperty]
    private bool _useAstrology;

    [ObservableProperty]
    private bool _announceFortune;

    public async Task InitializeAsync(Func<string, string, string, Task> alertFunc)
    {
        _alertFunc = alertFunc;

        try
        {
            var userProfileJson = await SecureStorage.GetAsync(Constants.USER_PROFILE_KEY);
            if (string.IsNullOrEmpty(userProfileJson))
            {
                return;
            }

            var userProfile = JsonSerializer.Deserialize<UserProfile>(userProfileJson);
            if (userProfile != null)
            {
                Name = userProfile.Name;
                Birthday = userProfile.Birthday?.ToShortDateString();
                UseAstrology = userProfile.UseAstrology;
                AnnounceFortune = userProfile.AnnounceFortune;
            }
        }
        catch (Exception)
        {
            // User profile must not exist yet
        }
    }

    [RelayCommand]
    private async Task Submit()
    {
        ArgumentNullException.ThrowIfNull(_alertFunc);

        if (string.IsNullOrWhiteSpace(Name))
        {
            await _alertFunc("Error", "Please enter at least your name.", "OK");
            return;
        }

        DateTime? birthday = null;
        if (!string.IsNullOrEmpty(Birthday) && !DateTime.TryParse(Birthday, out var parsedBirthday))
        {
            await _alertFunc("Error", "Invalid birthday format. Please use the format: MM/DD/YYYY", "OK");
            return;
        }

        var userProfile = new UserProfile
        {
            Name = Name,
            Birthday = birthday,
            UseAstrology = UseAstrology,
            AnnounceFortune = AnnounceFortune
        };

        await SecureStorage.SetAsync(Constants.USER_PROFILE_KEY, JsonSerializer.Serialize(userProfile));
        await Shell.Current.GoToAsync($"///{nameof(MainPage)}");
    }
}
