using Finance.Android17.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Android17.Maui;

public partial class ExpensesPage : ContentPage
{
    private readonly ExpensesViewModel _viewModel;
    private readonly IServiceProvider _services;
    public ExpensesPage(ExpensesViewModel viewModel, IServiceProvider services)
    {
        InitializeComponent(); BindingContext = _viewModel = viewModel; _services = services;
        _viewModel.EditRequested += OnEditRequested;
    }
    protected override bool OnBackButtonPressed()
    {
        if (!_viewModel.IsFilterPanelVisible) return base.OnBackButtonPressed();
        _viewModel.CloseFiltersCommand.Execute(null);
        return true;
    }
    protected override void OnAppearing() { base.OnAppearing(); _ = _viewModel.LoadAsync(); }
    private async void OnAddClicked(object? sender, EventArgs e) => await Navigation.PushModalAsync(new NavigationPage(_services.GetRequiredService<AddExpensePage>()));
    private async void OnEditRequested(object? sender, Guid expenseId)
    {
        var page = _services.GetRequiredService<AddExpensePage>();
        await page.BeginEditAsync(expenseId);
        await Navigation.PushModalAsync(new NavigationPage(page));
    }
}
