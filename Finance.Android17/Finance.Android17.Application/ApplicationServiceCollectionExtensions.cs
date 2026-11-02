using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Finance.Android17.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Enregistre les services métier de la couche Application.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<DueOperationsService>();
        services.AddSingleton<SubscriptionReminderScheduler>();
        services.AddSingleton<StartupService>();
        services.AddSingleton<FinanceDataLoader>();
        services.AddSingleton<DashboardService>();
        services.AddSingleton<NotificationService>();
        return services;
    }
}
