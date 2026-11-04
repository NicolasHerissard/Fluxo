using Finance.Android17.Application;
using Finance.Android17.Domain;
using Finance.Android17.Infrastructure;

namespace Finance.Android17.Tests.Support;

/// <summary>Horloge figée : la date « du jour » ne dépend jamais de la machine qui exécute les tests.</summary>
public sealed class FixedTimeProvider(DateTime localNow) : TimeProvider
{
    /// <summary>Heure courante (fuseau UTC) ; modifiable pour faire avancer le temps pendant un test.</summary>
    public DateTime Now { get; set; } = localNow;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(Now, DateTimeKind.Utc));
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
    public string Theme { get; set; } = ThemeOptions.FollowSystem;
    public bool AutoCreateSubscriptionExpenses { get; set; }
    public bool AutoCreateRecurringIncomes { get; set; } = true;
    public bool SubscriptionRemindersEnabled { get; set; } = true;
    public int SubscriptionReminderDays { get; set; } = 3;
    public long MonthlyBudgetMinor { get; set; }
    public string CurrencyCode { get; set; } = "EUR";
    public string DefaultCategoryId { get; set; } = string.Empty;
    public string LastCategoryId { get; set; } = string.Empty;
    public bool RememberLastCategory { get; set; } = true;
    public string AfterSaveBehavior { get; set; } = AfterSaveOptions.Close;
    public string SeenNotifications { get; set; } = string.Empty;
    public bool BackgroundAlertsEnabled { get; set; } = true;
    public string PushedNotifications { get; set; } = string.Empty;
    public string LastDailyCheckDay { get; set; } = string.Empty;
}

public sealed class FakeReminderService : IReminderService
{
    public List<ReminderPlan> Plans { get; } = [];

    public Task ApplyAsync(ReminderPlan plan, CancellationToken cancellationToken = default)
    {
        Plans.Add(plan);
        return Task.CompletedTask;
    }
}

/// <summary>Garde les lots d'alertes « envoyés » ; <see cref="Accepts"/> à faux simule une plateforme qui refuse de notifier.</summary>
public sealed class FakeAlertPublisher : IAlertPublisher
{
    public List<IReadOnlyList<AppNotification>> Batches { get; } = [];
    public bool Accepts { get; set; } = true;

    public Task<bool> PublishAsync(IReadOnlyList<AppNotification> alerts, CancellationToken cancellationToken = default)
    {
        if (Accepts) Batches.Add(alerts);
        return Task.FromResult(Accepts);
    }
}

public sealed class FakeDailyCheckScheduler : IDailyCheckScheduler
{
    public int Calls { get; private set; }

    public Task EnsureScheduledAsync(CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.CompletedTask;
    }
}

public sealed class FakeSecureValueStore : ISecureValueStore
{
    public Dictionary<string, string> Values { get; } = [];

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(Values.TryGetValue(key, out var value) ? value : null);

    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        Values[key] = value;
        return Task.CompletedTask;
    }
}

public sealed record SharedFile(string FileName, byte[] Content, string Title);

public sealed class FakeFileExchange : IFileExchange
{
    public List<SharedFile> Shared { get; } = [];
    public byte[]? ToPick { get; set; }

    public Task ShareAsync(string fileName, byte[] content, string title, CancellationToken cancellationToken = default)
    {
        Shared.Add(new SharedFile(fileName, content, title));
        return Task.CompletedTask;
    }

    public Task<byte[]?> PickAsync(string title, CancellationToken cancellationToken = default) => Task.FromResult(ToPick);
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

    /// <summary>Attente imposée après des échecs : tant qu'elle est positive, aucun PIN n'est accepté.</summary>
    public TimeSpan LockoutRemaining { get; set; }

    public Task<bool> VerifyPinAsync(string pin, CancellationToken cancellationToken = default) =>
        Task.FromResult(LockoutRemaining <= TimeSpan.Zero && Pin == pin);

    public Task<TimeSpan> GetLockoutRemainingAsync(CancellationToken cancellationToken = default) => Task.FromResult(LockoutRemaining);
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

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_directory, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
