using System.Globalization;
using System.Security.Cryptography;
using Finance.Android17.Application;

namespace Finance.Android17.Infrastructure;

/// <summary>
/// Code PIN de l'application. Seul un hachage PBKDF2-SHA256 salé est conservé (jamais le PIN), dans le coffre
/// de la plateforme. Les clés de stockage, le sel (16 octets) et les 100 000 itérations sont ceux des versions
/// déjà installées : les changer verrouillerait définitivement les utilisateurs existants.
/// Les échecs sont comptés dans le coffre (ils survivent à un redémarrage) : à partir de <see cref="FreeAttempts"/>
/// échecs de suite, chaque nouvel échec impose une attente qui double, de 30 secondes à 15 minutes.
/// </summary>
internal sealed class PinLockService(ISecureValueStore secrets, TimeProvider clock) : IAppLockService
{
    internal const string SaltKey = "pin_salt_v2";
    internal const string HashKey = "pin_hash_v2";
    internal const string AttemptsKey = "pin_attempts_v1";
    internal const int Iterations = 100_000;
    internal const int FreeAttempts = 5;
    private static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(15);
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public async Task<bool> HasPinAsync() => !string.IsNullOrEmpty(await secrets.GetAsync(HashKey).ConfigureAwait(false));

    public async Task SetPinAsync(string pin, CancellationToken cancellationToken = default)
    {
        if (!PinPolicy.IsValid(pin)) throw new ArgumentException($"Le PIN doit contenir {PinPolicy.RequirementText}.", nameof(pin));
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = await DeriveAsync(pin, salt, cancellationToken).ConfigureAwait(false);
        await _verifyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await secrets.SetAsync(SaltKey, Convert.ToBase64String(salt), cancellationToken).ConfigureAwait(false);
            await secrets.SetAsync(HashKey, Convert.ToBase64String(hash), cancellationToken).ConfigureAwait(false);
            await ClearAttemptsAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { _verifyGate.Release(); }
    }

    // Lecture du compteur, calcul puis écriture du nouvel échec forment une seule opération : deux vérifications
    // simultanées ne peuvent plus compter un seul échec, ni profiter d'une attente non encore enregistrée.
    private readonly SemaphoreSlim _verifyGate = new(1, 1);

    public async Task<bool> VerifyPinAsync(string pin, CancellationToken cancellationToken = default)
    {
        await _verifyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await VerifyCoreAsync(pin, cancellationToken).ConfigureAwait(false); }
        finally { _verifyGate.Release(); }
    }

    private async Task<bool> VerifyCoreAsync(string pin, CancellationToken cancellationToken)
    {
        var saltText = await secrets.GetAsync(SaltKey, cancellationToken).ConfigureAwait(false);
        var expectedText = await secrets.GetAsync(HashKey, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(saltText) || string.IsNullOrEmpty(expectedText)) return false;
        var attempts = await ReadAttemptsAsync(cancellationToken).ConfigureAwait(false);
        if (RemainingLockout(attempts) > TimeSpan.Zero) return false;

        var actual = await DeriveAsync(pin, Convert.FromBase64String(saltText), cancellationToken).ConfigureAwait(false);
        if (CryptographicOperations.FixedTimeEquals(actual, Convert.FromBase64String(expectedText)))
        {
            if (attempts.Failures > 0) await ClearAttemptsAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        var failed = new Attempts(attempts.Failures + 1, clock.GetUtcNow().ToUnixTimeMilliseconds());
        await secrets.SetAsync(AttemptsKey, $"{failed.Failures};{failed.LastFailureMs}", cancellationToken).ConfigureAwait(false);
        return false;
    }

    public async Task<TimeSpan> GetLockoutRemainingAsync(CancellationToken cancellationToken = default) =>
        RemainingLockout(await ReadAttemptsAsync(cancellationToken).ConfigureAwait(false));

    private TimeSpan RemainingLockout(Attempts attempts)
    {
        if (attempts.Failures < FreeAttempts) return TimeSpan.Zero;
        var delay = TimeSpan.FromTicks(Math.Min(MaxDelay.Ticks, FirstDelay.Ticks * (1L << Math.Min(attempts.Failures - FreeAttempts, 10))));
        var remaining = TimeSpan.FromMilliseconds(attempts.LastFailureMs) + delay - TimeSpan.FromMilliseconds(clock.GetUtcNow().ToUnixTimeMilliseconds());
        // Horloge ramenée en arrière : on applique l'attente entière plutôt que de la contourner.
        return remaining <= TimeSpan.Zero ? TimeSpan.Zero : TimeSpan.FromTicks(Math.Min(remaining.Ticks, delay.Ticks));
    }

    private async Task<Attempts> ReadAttemptsAsync(CancellationToken cancellationToken)
    {
        var text = await secrets.GetAsync(AttemptsKey, cancellationToken).ConfigureAwait(false);
        var parts = text?.Split(';');
        return parts is { Length: 2 }
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var failures)
            && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var lastFailureMs)
            ? new Attempts(failures, lastFailureMs)
            : new Attempts(0, 0);
    }

    private async Task ClearAttemptsAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(await secrets.GetAsync(AttemptsKey, cancellationToken).ConfigureAwait(false)))
            await secrets.SetAsync(AttemptsKey, "0;0", cancellationToken).ConfigureAwait(false);
    }

    private readonly record struct Attempts(int Failures, long LastFailureMs);

    // Le calcul PBKDF2 dure plusieurs dizaines de millisecondes : hors du thread d'interface.
    private static Task<byte[]> DeriveAsync(string pin, byte[] salt, CancellationToken cancellationToken) =>
        Task.Run(() => Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, HashSize), cancellationToken);
}
