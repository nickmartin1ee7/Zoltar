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
        await _vm.InitializeAsync();
        base.OnAppearing();
    }

    private async void SpecialInteraction_OnClicked(object sender, EventArgs e)
    {
        _vm.FortuneAllowed = true;
        await _vm.InvokeSpecialInteractionAsync();
    }
}