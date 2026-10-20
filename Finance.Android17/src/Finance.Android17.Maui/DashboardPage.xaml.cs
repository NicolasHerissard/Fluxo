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
    private void OnLockClicked(object? sender, EventArgs e) => (Microsoft.Maui.Controls.Application.Current as App)?.ShowLock();

    private async void OnSeeAllExpensesClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//expenses");

    private async void OnRecentExpenseTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not ExpenseRow row) return;
        var page = _services.GetRequiredService<AddExpensePage>();
        await page.BeginEditAsync(row.Source);
        await Navigation.PushModalAsync(new NavigationPage(page));
    }

    private void OnDashboardScrolled(object? sender, ScrolledEventArgs e)
    {
        var compact = e.ScrollY > 72;
        AddExpenseButton.Text = compact ? "+" : "+  Ajouter";
        AddExpenseButton.WidthRequest = compact ? 56 : 132;
    }
}
