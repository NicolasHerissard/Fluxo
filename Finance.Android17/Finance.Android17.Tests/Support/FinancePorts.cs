using Finance.Android17.Application;

namespace Finance.Android17.Tests.Support;

/// <summary>Accès direct, pour les tests, aux ports de persistance du conteneur d'injection.</summary>
public abstract class FinancePorts
{
    public abstract T Get<T>() where T : notnull;

    public IDatabaseInitializer Database => Get<IDatabaseInitializer>();
    public IExpenseRepository Expenses => Get<IExpenseRepository>();
    public ICategoryRepository Categories => Get<ICategoryRepository>();
    public IIncomeRepository Incomes => Get<IIncomeRepository>();
    public IRecurringIncomeRepository RecurringIncomes => Get<IRecurringIncomeRepository>();
    public ISubscriptionRepository Subscriptions => Get<ISubscriptionRepository>();
    public ISavingsGoalRepository SavingsGoals => Get<ISavingsGoalRepository>();
    public IFinancialProfileRepository Profile => Get<IFinancialProfileRepository>();
    public IDueOperationsStore DueOperations => Get<IDueOperationsStore>();
    public IFinanceDataStore Data => Get<IFinanceDataStore>();
}
