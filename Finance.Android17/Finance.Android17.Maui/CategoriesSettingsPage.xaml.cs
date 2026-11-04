using Finance.Android17.Presentation;

namespace Finance.Android17.Maui;

public partial class CategoriesSettingsPage : ContentPage
{
    private readonly CategoriesSettingsViewModel _viewModel;
    public CategoriesSettingsPage(CategoriesSettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }
    protected override void OnAppearing() { base.OnAppearing(); _ = _viewModel.LoadAsync(); }
    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopModalAsync();
}
