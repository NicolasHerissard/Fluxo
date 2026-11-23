using Finance.Android17.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Android17.Maui;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _viewModel;
    private readonly IServiceProvider _services;
    public DashboardPage(DashboardViewModel viewModel, IServiceProvider services) { InitializeComponent(); BindingContext = _viewModel = viewModel; _services = services; }
    protected override void OnAppearing() { base.OnAppearing(); _ = _viewModel.LoadAsync(); }
    private async void OnAddClicked(object? sender, EventArgs e) => await Navigation.PushModalAsync(new NavigationPage(_services.GetRequiredService<AddExpensePage>()));

    private async void OnScanClicked(object? sender, EventArgs e)
    {
        var page = _services.GetRequiredService<AddExpensePage>();
        page.PrepareForScan();
        await Navigation.PushModalAsync(new NavigationPage(page));
        await page.ScanReceiptAsync();
    }

    private async void OnSeeAllExpensesClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//expenses");

    private async void OnRecentExpenseTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not ExpenseRow row) return;
        var page = _services.GetRequiredService<AddExpensePage>();
        await page.BeginEditAsync(row.Id);
        await Navigation.PushModalAsync(new NavigationPage(page));
    }
}
