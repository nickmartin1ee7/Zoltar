using System.Text.Json;

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
            BirthdayEntry.Text = userProfile.Birthday.ToShortDateString();
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

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(birthdayText))
        {
            await DisplayAlert("Error", "Please enter both your name and birthday.", "OK");
            return;
        }

        if (!DateTime.TryParse(birthdayText, out DateTime birthday))
        {
            await DisplayAlert("Error", "Invalid birthday format. Please use the format: MM/DD/YYYY", "OK");
            return;
        }

        // Create a new UserProfile object with the validated data
        var userProfile = new UserProfile
        {
            Name = name,
            Birthday = birthday
        };

        await SecureStorage.SetAsync(Constants.USER_PROFILE_KEY, JsonSerializer.Serialize(userProfile));
        await Shell.Current.GoToAsync($"///{nameof(MainPage)}");
    }
}