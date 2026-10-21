using Finance.Android17.Presentation;

namespace Finance.Android17.Maui;

public partial class DataSettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;
    public DataSettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }
    protected override void OnAppearing() { base.OnAppearing(); _ = _viewModel.LoadAsync(); }
    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopModalAsync();

    private async void OnRestoreClicked(object? sender, EventArgs e)
    {
        if (await DisplayAlertAsync("Restaurer la sauvegarde ?", "Les données actuelles seront remplacées par celles du fichier sélectionné.", "Restaurer", "Annuler"))
            await _viewModel.RestoreBackupAsync();
    }
}
