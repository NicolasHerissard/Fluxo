using Finance.Android17.Application;
using Finance.Android17.Domain;
using Finance.Android17.Infrastructure;
using Finance.Android17.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Android17.Tests.Support;

/// <summary>
/// Application composée comme en production (<c>AddApplication</c>, <c>AddInfrastructure</c>, vraie base SQLCipher
/// temporaire), mais avec une horloge figée et des réglages/services natifs factices.
/// « Aujourd'hui » vaut le mardi 20 octobre 2026, 9 h 30.
/// </summary>
public sealed class AppFixture : FinancePorts, IDisposable
{
    public static readonly DateTime Now = new(2026, 10, 20, 9, 30, 0);

    private readonly TempDatabase _db = new();
    private readonly ServiceProvider _provider;

    public AppFixture(DateTime? now = null)
    {
        Clock = new FixedTimeProvider(now ?? Now);
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(Clock);
        services.AddSingleton<ISecureValueStore>(new FakeSecureValueStore());
        services.AddSingleton<IFileExchange>(new FakeFileExchange());
        services.AddSingleton<ISettingsService>(Settings);
        services.AddSingleton<IReminderService>(Reminders);
        services.AddSingleton<IBiometricService>(new FakeBiometrics());
        services.AddSingleton<IOcrService>(new FakeOcrService());
        services.AddInfrastructure(_db.Options);
        services.AddApplication();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    public FixedTimeProvider Clock { get; }
    public FakeSettings Settings { get; } = new();
    public FakeReminderService Reminders { get; } = new();

    public override T Get<T>() => _provider.GetRequiredService<T>();

    /// <summary>Construit un ViewModel avec ses dépendances du conteneur ; <paramref name="overrides"/> remplace celles de même type.</summary>
    private T Create<T>(params object[] overrides) => ActivatorUtilities.CreateInstance<T>(_provider, overrides);

    public DashboardViewModel CreateDashboard() => Create<DashboardViewModel>();
    public ExpensesViewModel CreateExpenses() => Create<ExpensesViewModel>();
    public IncomeViewModel CreateIncome() => Create<IncomeViewModel>();
    public SettingsViewModel CreateSettings() => Create<SettingsViewModel>(new FakeAppLock(), new FakeBiometrics());
    public SubscriptionsViewModel CreateSubscriptions() => Create<SubscriptionsViewModel>();
    public AddExpenseViewModel CreateAddExpense(string? ocrText = null) => Create<AddExpenseViewModel>(new FakeOcrService(ocrText));
    public LockViewModel CreateLock(FakeAppLock appLock, FakeBiometrics? biometrics = null) =>
        Create<LockViewModel>(appLock, biometrics ?? new FakeBiometrics());

    public async Task<Category> CategoryAsync(string name) =>
        (await Categories.GetAllAsync()).Single(x => x.Name == name);

    /// <summary>
    /// Scénario commun : octobre 2026 contient 3 dépenses (202,50 €), septembre une (80,00 €), un salaire de
    /// 2 000 €, l'abonnement Netflix (15,99 € le 25/10) et le revenu récurrent « Loyer perçu » (50 € le 30/10).
    /// Le budget de la catégorie Alimentation est de 200 €.
    /// </summary>
    public async Task SeedScenarioAsync()
    {
        var food = await CategoryAsync("Alimentation");
        var transport = await CategoryAsync("Transport");
        await Categories.UpdateAsync(food.Id, food.Name, food.Color, 20_000);
        await Expenses.AddAsync(1_250, "Pain", new DateTime(2026, 10, 2), food.Id);
        await Expenses.AddAsync(15_000, "Courses", new DateTime(2026, 10, 10), food.Id);
        await Expenses.AddAsync(4_000, "Train", new DateTime(2026, 10, 15), transport.Id);
        await Expenses.AddAsync(8_000, "Marché", new DateTime(2026, 9, 10), food.Id);
        await Incomes.AddAsync(200_000, "Salaire", new DateTime(2026, 10, 1));
        await Subscriptions.AddAsync("Netflix", 1_599, 1, new DateTime(2026, 10, 25));
        await RecurringIncomes.AddAsync("Loyer perçu", 5_000, 1, new DateTime(2026, 10, 30));
    }

    public void Dispose()
    {
        _provider.Dispose();
        _db.Dispose();
    }
}

public static class TestText
{
    /// <summary>Remplace les espaces insécables que produit la culture fr-FR par des espaces simples.</summary>
    public static string Fr(this string value) => value.Replace(' ', ' ').Replace(' ', ' ');

    /// <summary>Supprime tous les espaces : le pourcentage dépend de la culture courante (« 80 % » ou « 80% »).</summary>
    public static string Compact(this string value) => new(value.Where(c => !char.IsWhiteSpace(c)).ToArray());

    public static async Task WaitUntilAsync(Func<bool> condition, int timeoutMilliseconds = 5_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("La condition attendue n'a jamais été vraie.");
            await Task.Delay(20);
        }
    }
}
