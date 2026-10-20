using Finance.Android17.Presentation;

namespace Finance.Android17.Maui;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        VersionLabel.Text = $"Mes Finances {AppInfo.Current.VersionString} • Données chiffrées localement";
    }
    protected override void OnAppearing() { base.OnAppearing(); _ = _viewModel.LoadAsync(); }
    private void OnLockClicked(object? sender, EventArgs e) => (Microsoft.Maui.Controls.Application.Current as App)?.ShowLock();

    private async void OnRestoreClicked(object? sender, EventArgs e)
    {
        if (await DisplayAlertAsync("Restaurer la sauvegarde ?", "Les données actuelles seront remplacées par celles du fichier sélectionné.", "Restaurer", "Annuler"))
            await _viewModel.RestoreBackupAsync();
    }

    private async void OnDeleteAllClicked(object? sender, EventArgs e)
    {
        if (!await DisplayAlertAsync("Supprimer toutes les données ?", "Toutes les dépenses, catégories personnalisées et abonnements seront supprimés. Cette action est irréversible.", "Continuer", "Annuler")) return;
        if (!await DisplayAlertAsync("Confirmation définitive", "Voulez-vous vraiment remettre les données financières à zéro ?", "Tout supprimer", "Annuler")) return;
        var pin = await DisplayPromptAsync("Authentification requise", "Saisissez votre code PIN pour confirmer la suppression.", "Confirmer", "Annuler", keyboard: Keyboard.Numeric, maxLength: 8);
        if (!string.IsNullOrEmpty(pin)) await _viewModel.DeleteAllDataAsync(pin);
    }
}
