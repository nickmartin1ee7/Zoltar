namespace Zoltar;

public partial class MainPage : ContentPage
{
    private readonly MainPageViewModel _vm;

    public MainPage(MainPageViewModel mainPageViewModel)
    {
        BindingContext = _vm = mainPageViewModel;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitializeAsync();

        if (Application.Current?.Windows?.FirstOrDefault() is not null)
        {
            Application.Current.Windows[0].Deactivated -= Window_OnDeactivating;
            Application.Current.Windows[0].Deactivated += Window_OnDeactivating;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
       
        if (Application.Current?.Windows?.FirstOrDefault() is not null)
        {
            Application.Current.Windows[0].Deactivated -= Window_OnDeactivating;
        }
    }

    private void Window_OnDeactivating(object? sender, EventArgs e)
    {
        _vm.TryCancelAnnounceFortuneCommand.Execute(this);
    }

    private async void SpecialInteraction_OnClicked(object sender, EventArgs e)
    {
        await _vm.InvokeSpecialInteractionAsync();
    }
}