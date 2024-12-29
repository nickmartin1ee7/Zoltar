using System.Text.Json;

using Zoltar.Models;

namespace Zoltar;

public partial class OnboardingPage : ContentPage
{
    public OnboardingPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            var userProfileJson = await SecureStorage.GetAsync(Constants.USER_PROFILE_KEY);

            if (string.IsNullOrEmpty(userProfileJson))
                return;

            var userProfile = JsonSerializer.Deserialize<UserProfile>(userProfileJson)!;

            NameEntry.Text = userProfile.Name;
            BirthdayEntry.Text = userProfile.Birthday.HasValue ? userProfile.Birthday.Value.ToShortDateString() : null;
            UseAstrologyBtn.IsChecked = userProfile.UseAstrology;
            AnnounceFortuneBtn.IsChecked = userProfile.AnnounceFortune;
        }
        catch (Exception)
        {
            // User profile must not exist yet
        }
    }

    public async void OnSubmitButtonClicked(object sender, EventArgs e)
    {
        var name = NameEntry.Text;
        var birthdayText = BirthdayEntry.Text;

        // Minimum requirement, their name
        if (string.IsNullOrWhiteSpace(name))
        {
            await DisplayAlert("Error", "Please enter at least your name.", "OK");
            return;
        }

        // Bday is optional
        var hasBirthday = birthdayText is { Length: > 0 };
        DateTime birthday = default;
        if (hasBirthday && !DateTime.TryParse(birthdayText, out birthday))
        {
            await DisplayAlert("Error", "Invalid birthday format. Please use the format: MM/DD/YYYY", "OK");
            return;
        }

        // Create a new UserProfile object with the validated data
        var userProfile = new UserProfile
        {
            Name = name,
            Birthday = hasBirthday ? birthday : null,
            UseAstrology = UseAstrologyBtn.IsChecked,
            AnnounceFortune = AnnounceFortuneBtn.IsChecked
        };

        await SecureStorage.SetAsync(Constants.USER_PROFILE_KEY, JsonSerializer.Serialize(userProfile));
        await Shell.Current.GoToAsync($"///{nameof(MainPage)}");
    }

    private void BirthdayEntry_TextChanged(object sender, TextChangedEventArgs e)
    {
        UseAstrologyBtn.IsEnabled = e.NewTextValue.Length > 0;
        UseAstrologyBtn.IsChecked = UseAstrologyBtn.IsEnabled && UseAstrologyBtn.IsChecked;
    }
}