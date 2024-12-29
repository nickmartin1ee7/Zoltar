namespace Zoltar;

public partial class OnboardingPage : ContentPage
{
    private readonly OnboardingViewModel _vm;

    public OnboardingPage(OnboardingViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        ArgumentNullException.ThrowIfNull(Application.Current?.MainPage);
        await _vm.InitializeAsync(Application.Current.MainPage.DisplayAlert);
    }
}
