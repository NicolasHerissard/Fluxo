using Finance.Android17.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Finance.Android17.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Enregistre la base chiffrée et ses dépôts (un par agrégat), la sécurité (PIN, clé de base), l'export CSV et la sauvegarde chiffrée.
    /// L'hôte doit fournir <see cref="ISecureValueStore"/> et <see cref="IFileExchange"/> (adaptateurs de plateforme).
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, FinanceDatabaseOptions? database = null)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(database ?? FinanceDatabaseOptions.Default());
        services.AddSingleton<ISecretStore, DatabaseKeyProvider>();
        services.AddSingleton<IAppLockService, PinLockService>();
        services.AddSingleton<IDatabaseRecovery, DatabaseRecovery>();
        services.AddSingleton<FinanceDatabase>();
        services.AddSingleton<IDatabaseInitializer>(provider => provider.GetRequiredService<FinanceDatabase>());
        services.AddSingleton<IExpenseRepository, ExpenseRepository>();
        services.AddSingleton<ICategoryRepository, CategoryRepository>();
        services.AddSingleton<IIncomeRepository, IncomeRepository>();
        services.AddSingleton<IRecurringIncomeRepository, RecurringIncomeRepository>();
        services.AddSingleton<ISubscriptionRepository, SubscriptionRepository>();
        services.AddSingleton<ISavingsGoalRepository, SavingsGoalRepository>();
        services.AddSingleton<IFinancialProfileRepository, FinancialProfileRepository>();
        services.AddSingleton<IDueOperationsStore, DueOperationsStore>();
        services.AddSingleton<IFinanceDataStore, FinanceDataStore>();
        services.AddSingleton<IExportService, CsvExportService>();
        services.AddSingleton<IDataBackupService, EncryptedBackupService>();
        return services;
    }
}
