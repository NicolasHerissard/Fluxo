using Finance.Android17.Presentation;

namespace Finance.Android17.Maui;

public partial class IncomePage : ContentPage
{
    private readonly IncomeViewModel _viewModel;

    public IncomePage(IncomeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _viewModel.LoadAsync();
    }
}
