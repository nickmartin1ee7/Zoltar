namespace Zoltar;

public partial class PreviousFortunesPage : ContentPage
{
    private readonly PreviousFortunesViewModel _vm;

    public PreviousFortunesPage(PreviousFortunesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _vm = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitializeAsync();
    }
}