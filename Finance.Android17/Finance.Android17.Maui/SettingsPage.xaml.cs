using Finance.Android17.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Android17.Maui;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;
    private readonly IServiceProvider _services;
    public SettingsPage(SettingsViewModel viewModel, UpdateViewModel updates, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _services = services;
        UpdateCard.BindingContext = updates;
        updates.ConfirmInstallAsync = (title, message, accept) => DisplayAlertAsync(title, message, accept, "Annuler");
        VersionLabel.Text = $"Fluxo {AppInfo.Current.VersionString} • Données chiffrées localement";
    }
    protected override void OnAppearing() { base.OnAppearing(); _ = _viewModel.LoadAsync(); }
    private void OnLockClicked(object? sender, EventArgs e) => (Microsoft.Maui.Controls.Application.Current as App)?.ShowLock();

    private async void OnOpenCategoriesTapped(object? sender, TappedEventArgs e) => await Navigation.PushModalAsync(_services.GetRequiredService<CategoriesSettingsPage>());
    private async void OnOpenDataTapped(object? sender, TappedEventArgs e) => await Navigation.PushModalAsync(_services.GetRequiredService<DataSettingsPage>());
}
