using Finance.Android17.Presentation;

namespace Finance.Android17.Maui;

public partial class DataSettingsPage : ContentPage
{
    private readonly DataSettingsViewModel _viewModel;
    public DataSettingsPage(DataSettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }
    protected override void OnAppearing() { base.OnAppearing(); _ = _viewModel.LoadAsync(); }
    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopModalAsync();

    private async void OnRestoreClicked(object? sender, EventArgs e)
    {
        if (await DisplayAlertAsync("Importer toutes les données ?", "Toutes vos données actuelles (transactions, catégories, abonnements, revenus, objectifs, solde de départ) et vos réglages seront remplacés par ceux du fichier choisi. Cette action est irréversible : exportez d’abord vos données si vous voulez pouvoir revenir en arrière.", "Importer", "Annuler"))
            await _viewModel.RestoreBackupAsync();
    }

    private async void OnDeleteAllClicked(object? sender, EventArgs e)
    {
        if (!await DisplayAlertAsync("Supprimer toutes les données ?", "Toutes les transactions, catégories personnalisées et abonnements seront supprimés. Cette action est irréversible.", "Continuer", "Annuler")) return;
        if (!await DisplayAlertAsync("Confirmation définitive", "Voulez-vous vraiment remettre les données financières à zéro ?", "Tout supprimer", "Annuler")) return;
        var pin = await DisplayPromptAsync("Authentification requise", "Saisissez votre code PIN pour confirmer la suppression.", "Confirmer", "Annuler", keyboard: Keyboard.Numeric, maxLength: 8);
        if (!string.IsNullOrEmpty(pin)) await _viewModel.DeleteAllDataAsync(pin);
    }
}
