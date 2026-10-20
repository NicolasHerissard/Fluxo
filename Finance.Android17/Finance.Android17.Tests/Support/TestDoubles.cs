using Finance.Android17.Application;
using Finance.Android17.Domain;
using Finance.Android17.Infrastructure;

namespace Finance.Android17.Tests.Support;

/// <summary>Horloge figée : la date « du jour » ne dépend jamais de la machine qui exécute les tests.</summary>
public sealed class FixedTimeProvider(DateTime localNow) : TimeProvider
{
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(localNow, DateTimeKind.Utc));
}

public sealed class FakeSecretStore(string key = "cle-de-test-0123456789abcdef0123456789=") : ISecretStore
{
    public Task<string> GetOrCreateDatabaseKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult(key);
}

/// <summary>Réglages en mémoire, avec les mêmes valeurs par défaut que <c>AppSettingsService</c>.</summary>
public sealed class FakeSettings : ISettingsService
{
    public bool HideAmounts { get; set; }
    public bool BiometricsEnabled { get; set; }
    public int AutoLockMinutes { get; set; } = 5;
    public string Theme { get; set; } = "Système";
    public bool AutoCreateSubscriptionExpenses { get; set; }
    public bool AutoCreateRecurringIncomes { get; set; } = true;
    public bool SubscriptionRemindersEnabled { get; set; } = true;
    public int SubscriptionReminderDays { get; set; } = 3;
    public long MonthlyBudgetMinor { get; set; }
    public string CurrencyCode { get; set; } = "EUR";
    public string DefaultCategoryId { get; set; } = string.Empty;
    public string LastCategoryId { get; set; } = string.Empty;
    public bool RememberLastCategory { get; set; } = true;
    public string AfterSaveBehavior { get; set; } = "Fermer";
}

public sealed class FakeReminderService : IReminderService
{
    public List<(int Count, int Days)> Calls { get; } = [];

    public Task ShowSubscriptionReminderAsync(IReadOnlyList<Subscription> subscriptions, int reminderDays, CancellationToken cancellationToken = default)
    {
        Calls.Add((subscriptions.Count, reminderDays));
        return Task.CompletedTask;
    }
}

public sealed class FakeAppLock(string? pin = null) : IAppLockService
{
    public string? Pin { get; private set; } = pin;

    public Task<bool> HasPinAsync() => Task.FromResult(Pin is not null);

    public Task SetPinAsync(string pin, CancellationToken cancellationToken = default)
    {
        Pin = pin;
        return Task.CompletedTask;
    }

    public Task<bool> VerifyPinAsync(string pin, CancellationToken cancellationToken = default) => Task.FromResult(Pin == pin);
}

public sealed class FakeBiometrics(bool isAvailable = false, bool succeeds = true) : IBiometricService
{
    public bool IsAvailable => isAvailable;
    public Task<bool> AuthenticateAsync(string reason, CancellationToken cancellationToken = default) => Task.FromResult(succeeds);
}

public sealed class FakeOcrService(string? text = null) : IOcrService
{
    public Task<OcrTextResult?> RecognizeAsync(string imagePath, CancellationToken cancellationToken = default) =>
        Task.FromResult(text is null ? null : new OcrTextResult(text));
}

/// <summary>Base SQLCipher temporaire, supprimée avec le test.</summary>
public sealed class TempDatabase : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "fluxo-tests-" + Guid.NewGuid().ToString("N"));

    public TempDatabase() => Directory.CreateDirectory(_directory);

    public FinanceDatabaseOptions Options => new(Path.Combine(_directory, "finance-test.db3"));

    public EncryptedFinanceStore CreateStore(string? key = null) =>
        new(key is null ? new FakeSecretStore() : new FakeSecretStore(key), Options);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_directory, true); } catch (IOException) { }
    }
}
