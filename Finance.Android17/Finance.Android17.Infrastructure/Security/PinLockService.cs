using System.Security.Cryptography;
using Finance.Android17.Application;

namespace Finance.Android17.Infrastructure;

/// <summary>
/// Code PIN de l'application. Seul un hachage PBKDF2-SHA256 salé est conservé (jamais le PIN), dans le coffre
/// de la plateforme. Les clés de stockage, le sel (16 octets) et les 100 000 itérations sont ceux des versions
/// déjà installées : les changer verrouillerait définitivement les utilisateurs existants.
/// </summary>
internal sealed class PinLockService(ISecureValueStore secrets) : IAppLockService
{
    internal const string SaltKey = "pin_salt_v2";
    internal const string HashKey = "pin_hash_v2";
    internal const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public async Task<bool> HasPinAsync() => !string.IsNullOrEmpty(await secrets.GetAsync(HashKey).ConfigureAwait(false));

    public async Task SetPinAsync(string pin, CancellationToken cancellationToken = default)
    {
        if (!PinPolicy.IsValid(pin)) throw new ArgumentException($"Le PIN doit contenir {PinPolicy.RequirementText}.", nameof(pin));
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = await DeriveAsync(pin, salt, cancellationToken).ConfigureAwait(false);
        await secrets.SetAsync(SaltKey, Convert.ToBase64String(salt), cancellationToken).ConfigureAwait(false);
        await secrets.SetAsync(HashKey, Convert.ToBase64String(hash), cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> VerifyPinAsync(string pin, CancellationToken cancellationToken = default)
    {
        var saltText = await secrets.GetAsync(SaltKey, cancellationToken).ConfigureAwait(false);
        var expectedText = await secrets.GetAsync(HashKey, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(saltText) || string.IsNullOrEmpty(expectedText)) return false;
        var actual = await DeriveAsync(pin, Convert.FromBase64String(saltText), cancellationToken).ConfigureAwait(false);
        return CryptographicOperations.FixedTimeEquals(actual, Convert.FromBase64String(expectedText));
    }

    // Le calcul PBKDF2 dure plusieurs dizaines de millisecondes : hors du thread d'interface.
    private static Task<byte[]> DeriveAsync(string pin, byte[] salt, CancellationToken cancellationToken) =>
        Task.Run(() => Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, HashSize), cancellationToken);
}
