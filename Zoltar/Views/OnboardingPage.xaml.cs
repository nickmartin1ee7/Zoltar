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

        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null)
        {
            return;
        }

        await _vm.InitializeAsync(
            (title, message, accept) => page.DisplayAlertAsync(title, message, accept));
    }
}
