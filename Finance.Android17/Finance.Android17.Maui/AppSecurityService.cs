using System.Security.Cryptography;
using Finance.Android17.Application;

namespace Finance.Android17.Maui;

public sealed class AppSecurityService : IAppLockService, ISecretStore
{
    private const string PinSaltKey = "pin_salt_v2";
    private const string PinHashKey = "pin_hash_v2";
    private const string DatabaseKey = "database_key_v2";
    private const int Iterations = 100_000;

    public async Task<bool> HasPinAsync() => !string.IsNullOrEmpty(await SecureStorage.Default.GetAsync(PinHashKey));

    public async Task SetPinAsync(string pin, CancellationToken cancellationToken = default)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = await DeriveAsync(pin, salt, cancellationToken).ConfigureAwait(false);
        await SecureStorage.Default.SetAsync(PinSaltKey, Convert.ToBase64String(salt));
        await SecureStorage.Default.SetAsync(PinHashKey, Convert.ToBase64String(hash));
    }

    public async Task<bool> VerifyPinAsync(string pin, CancellationToken cancellationToken = default)
    {
        var saltText = await SecureStorage.Default.GetAsync(PinSaltKey);
        var expectedText = await SecureStorage.Default.GetAsync(PinHashKey);
        if (string.IsNullOrEmpty(saltText) || string.IsNullOrEmpty(expectedText)) return false;
        var actual = await DeriveAsync(pin, Convert.FromBase64String(saltText), cancellationToken).ConfigureAwait(false);
        return CryptographicOperations.FixedTimeEquals(actual, Convert.FromBase64String(expectedText));
    }

    public async Task<string> GetOrCreateDatabaseKeyAsync(CancellationToken cancellationToken = default)
    {
        var existing = await SecureStorage.Default.GetAsync(DatabaseKey);
        if (!string.IsNullOrEmpty(existing)) return existing;
        cancellationToken.ThrowIfCancellationRequested();
        var created = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await SecureStorage.Default.SetAsync(DatabaseKey, created);
        return created;
    }

    private static Task<byte[]> DeriveAsync(string pin, byte[] salt, CancellationToken cancellationToken) =>
        Task.Run(() => Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, 32), cancellationToken);
}
