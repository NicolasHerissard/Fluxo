using Finance.Android17.Presentation;

namespace Finance.Android17.Maui;

public partial class SubscriptionsPage : ContentPage
{
    private readonly SubscriptionsViewModel _viewModel;
    public SubscriptionsPage(SubscriptionsViewModel viewModel) { InitializeComponent(); BindingContext = _viewModel = viewModel; }
    protected override void OnAppearing() { base.OnAppearing(); _ = _viewModel.LoadAsync(); }
}
