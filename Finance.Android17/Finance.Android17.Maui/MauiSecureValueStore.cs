using Finance.Android17.Application;

namespace Finance.Android17.Maui;

/// <summary>Coffre à secrets de la plateforme (<c>SecureStorage</c> : Keystore Android).</summary>
public sealed class MauiSecureValueStore : ISecureValueStore
{
    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) => SecureStorage.Default.GetAsync(key);

    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default) => SecureStorage.Default.SetAsync(key, value);
}
