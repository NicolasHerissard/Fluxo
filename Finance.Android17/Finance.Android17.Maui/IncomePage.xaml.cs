using Finance.Android17.Presentation;

namespace Finance.Android17.Maui;

public partial class IncomePage : ContentPage
{
    private readonly IncomeViewModel _viewModel;

    public IncomePage(IncomeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.ConfirmDeleteAsync = (title, message) => DisplayAlertAsync(title, message, "Supprimer", "Annuler");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _viewModel.LoadAsync();
    }
}
