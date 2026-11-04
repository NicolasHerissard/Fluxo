using Finance.Android17.Presentation;

namespace Finance.Android17.Maui;

public partial class LockPage : ContentPage
{
    private readonly LockViewModel _viewModel;
    public event EventHandler? Unlocked;

    public LockPage(LockViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.Unlocked += (_, _) => Unlocked?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _viewModel.InitializeAsync();
    }

    private async void OnStartOverClicked(object? sender, EventArgs e)
    {
        if (!await DisplayAlertAsync("Repartir d’une base neuve ?", "Vos données actuelles sont illisibles sans leur clé. Elles sont mises de côté sur l’appareil, pas supprimées, et l’application démarre avec une base vide. Vous pourrez ensuite restaurer une sauvegarde chiffrée depuis Réglages > Mes données.", "Repartir de zéro", "Annuler")) return;
        await _viewModel.StartOverAsync();
    }
}
