using Microsoft.Extensions.DependencyInjection;

namespace Finance.Android17.Presentation;

public static class PresentationServiceCollectionExtensions
{
    /// <summary>
    /// Enregistre les ViewModels. Ils sont transients : les vues MAUI ne doivent jamais être réutilisées après le
    /// remplacement de <c>Window.Page</c> (leur contexte Android est alors détruit), donc leur ViewModel non plus.
    /// </summary>
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddTransient<LockViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ExpensesViewModel>();
        services.AddTransient<IncomeViewModel>();
        services.AddTransient<SubscriptionsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<CategoriesSettingsViewModel>();
        services.AddTransient<DataSettingsViewModel>();
        services.AddTransient<AddExpenseViewModel>();
        services.AddTransient<NotificationsViewModel>();
        return services;
    }
}
