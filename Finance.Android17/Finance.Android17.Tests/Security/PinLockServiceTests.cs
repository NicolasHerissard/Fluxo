using System.Security.Cryptography;
using Finance.Android17.Application;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Security;

public sealed class PinLockServiceTests : IDisposable
{
    private readonly InfrastructureHost _host = new();

    public void Dispose() => _host.Dispose();

    private IAppLockService Lock => _host.Get<IAppLockService>();

    [Fact]
    public async Task HasPin_IsFalseUntilAPinIsSet()
    {
        Assert.False(await Lock.HasPinAsync());

        await Lock.SetPinAsync("4821");

        Assert.True(await Lock.HasPinAsync());
    }

    [Fact]
    public async Task VerifyPin_AcceptsOnlyTheRightPin()
    {
        await Lock.SetPinAsync("4821");

        Assert.True(await Lock.VerifyPinAsync("4821"));
        Assert.False(await Lock.VerifyPinAsync("4822"));
        Assert.False(await Lock.VerifyPinAsync(string.Empty));
    }

    [Fact]
    public async Task VerifyPin_IsFalse_WhenNoPinWasEverSet()
    {
        Assert.False(await Lock.VerifyPinAsync("4821"));
    }

    [Fact]
    public async Task SetPin_ReplacesTheOldPin()
    {
        await Lock.SetPinAsync("4821");
        await Lock.SetPinAsync("99887766");

        Assert.False(await Lock.VerifyPinAsync("4821"));
        Assert.True(await Lock.VerifyPinAsync("99887766"));
    }

    [Fact]
    public async Task SetPin_StoresASaltedHash_NeverThePin()
    {
        await Lock.SetPinAsync("4821");

        Assert.Equal(["pin_hash_v2", "pin_salt_v2"], _host.Secrets.Values.Keys.Order());
        Assert.Equal(16, Convert.FromBase64String(_host.Secrets.Values["pin_salt_v2"]).Length);
        Assert.Equal(32, Convert.FromBase64String(_host.Secrets.Values["pin_hash_v2"]).Length);
        Assert.DoesNotContain(_host.Secrets.Values.Values, value => value.Contains("4821"));
    }

    [Fact]
    public async Task SetPin_UsesAFreshSaltEveryTime()
    {
        await Lock.SetPinAsync("4821");
        var firstSalt = _host.Secrets.Values["pin_salt_v2"];
        var firstHash = _host.Secrets.Values["pin_hash_v2"];

        await Lock.SetPinAsync("4821");

        Assert.NotEqual(firstSalt, _host.Secrets.Values["pin_salt_v2"]);
        Assert.NotEqual(firstHash, _host.Secrets.Values["pin_hash_v2"]);
    }

    [Fact]
    public async Task VerifyPin_AcceptsAHashWrittenByAnEarlierVersion()
    {
        // Sel 00..0F et PIN « 4821 » : PBKDF2-SHA256, 100 000 itérations, 32 octets (calculé hors de l'application).
        _host.Secrets.Values["pin_salt_v2"] = "AAECAwQFBgcICQoLDA0ODw==";
        _host.Secrets.Values["pin_hash_v2"] = "wmpH7CpIGbsLmht6VKVaCLZoFloVcHYAdmA4DgEdAUg=";

        Assert.True(await Lock.HasPinAsync());
        Assert.True(await Lock.VerifyPinAsync("4821"));
        Assert.False(await Lock.VerifyPinAsync("1234"));
    }

    [Fact]
    public async Task StoredHash_MatchesPbkdf2Sha256With100000Iterations()
    {
        await Lock.SetPinAsync("4821");

        var salt = Convert.FromBase64String(_host.Secrets.Values["pin_salt_v2"]);
        var expected = Rfc2898DeriveBytes.Pbkdf2("4821", salt, 100_000, HashAlgorithmName.SHA256, 32);

        Assert.Equal(expected, Convert.FromBase64String(_host.Secrets.Values["pin_hash_v2"]));
    }
}
