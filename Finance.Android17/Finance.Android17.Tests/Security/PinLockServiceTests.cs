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

    private async Task FailAsync(int times)
    {
        for (var i = 0; i < times; i++) Assert.False(await Lock.VerifyPinAsync("0000"));
    }

    [Fact]
    public async Task FirstFourWrongPins_AreNotDelayed()
    {
        await Lock.SetPinAsync("4821");

        await FailAsync(4);

        Assert.Equal(TimeSpan.Zero, await Lock.GetLockoutRemainingAsync());
        Assert.True(await Lock.VerifyPinAsync("4821"));
    }

    [Fact]
    public async Task FifthWrongPin_LocksFor30Seconds_EvenForTheRightPin()
    {
        await Lock.SetPinAsync("4821");

        await FailAsync(5);

        Assert.Equal(TimeSpan.FromSeconds(30), await Lock.GetLockoutRemainingAsync());
        Assert.False(await Lock.VerifyPinAsync("4821"));
        _host.Clock.Now = _host.Clock.Now.AddSeconds(31);
        Assert.Equal(TimeSpan.Zero, await Lock.GetLockoutRemainingAsync());
        Assert.True(await Lock.VerifyPinAsync("4821"));
    }

    [Fact]
    public async Task EachFurtherFailure_DoublesTheWait_UpToFifteenMinutes()
    {
        await Lock.SetPinAsync("4821");
        var expected = new[] { 30, 60, 120, 240, 480, 900, 900 };

        for (var i = 0; i < expected.Length; i++)
        {
            await FailAsync(i == 0 ? 5 : 1);
            Assert.Equal(TimeSpan.FromSeconds(expected[i]), await Lock.GetLockoutRemainingAsync());
            _host.Clock.Now = _host.Clock.Now.AddSeconds(expected[i] + 1);
        }
    }

    [Fact]
    public async Task Failures_SurviveARestart_AndASuccessResetsThem()
    {
        await Lock.SetPinAsync("4821");
        await FailAsync(5);
        using var restarted = new InfrastructureHost(secrets: _host.Secrets, databaseOf: _host);
        var lockAfterRestart = restarted.Get<IAppLockService>();

        Assert.True(await lockAfterRestart.GetLockoutRemainingAsync() > TimeSpan.Zero);

        restarted.Clock.Now = restarted.Clock.Now.AddMinutes(1);
        Assert.True(await lockAfterRestart.VerifyPinAsync("4821"));
        Assert.Equal(TimeSpan.Zero, await lockAfterRestart.GetLockoutRemainingAsync());
        Assert.False(await lockAfterRestart.VerifyPinAsync("0000"));
        Assert.Equal(TimeSpan.Zero, await lockAfterRestart.GetLockoutRemainingAsync());
    }

    [Fact]
    public async Task AClockSetBackwards_DoesNotShortenTheWait()
    {
        await Lock.SetPinAsync("4821");
        await FailAsync(5);

        _host.Clock.Now = _host.Clock.Now.AddHours(-5);

        Assert.Equal(TimeSpan.FromSeconds(30), await Lock.GetLockoutRemainingAsync());
    }

    [Fact]
    public async Task StoredHash_MatchesPbkdf2Sha256With100000Iterations()
    {
        await Lock.SetPinAsync("4821");

        var salt = Convert.FromBase64String(_host.Secrets.Values["pin_salt_v2"]);
        var expected = Rfc2898DeriveBytes.Pbkdf2("4821", salt, 100_000, HashAlgorithmName.SHA256, 32);

        Assert.Equal(expected, Convert.FromBase64String(_host.Secrets.Values["pin_hash_v2"]));
    }

    [Fact]
    public async Task ConcurrentWrongAttempts_AreAllCounted()
    {
        await Lock.SetPinAsync("4821");

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => Lock.VerifyPinAsync("0000"))));

        Assert.All(results, Assert.False);
        Assert.Equal("4;" + _host.Clock.GetUtcNow().ToUnixTimeMilliseconds(), _host.Secrets.Values["pin_attempts_v1"]);
    }
}
