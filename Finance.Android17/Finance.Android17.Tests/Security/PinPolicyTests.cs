using Finance.Android17.Application;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Security;

public sealed class PinPolicyTests
{
    [Theory]
    [InlineData("1234")]
    [InlineData("12345678")]
    [InlineData("0000")]
    [InlineData("048152")]
    public void IsValid_AcceptsFourToEightDigits(string pin) => Assert.True(PinPolicy.IsValid(pin));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("123456789")]
    [InlineData("12a4")]
    [InlineData("12 34")]
    [InlineData("-1234")]
    [InlineData("١٢٣٤")] // chiffres arabo-indiens : des chiffres Unicode, pas des chiffres ASCII
    public void IsValid_RejectsEverythingElse(string? pin) => Assert.False(PinPolicy.IsValid(pin));

    [Fact]
    public void Requirement_IsWorded_FromTheBounds()
    {
        Assert.Equal("entre 4 et 8 chiffres", PinPolicy.RequirementText);
    }

    [Fact]
    public async Task PinService_RefusesToStoreAnInvalidPin()
    {
        using var host = new InfrastructureHost();

        var error = await Assert.ThrowsAsync<ArgumentException>(() => host.Get<IAppLockService>().SetPinAsync("12"));

        Assert.StartsWith("Le PIN doit contenir entre 4 et 8 chiffres.", error.Message);
        Assert.False(await host.Get<IAppLockService>().HasPinAsync());
    }
}
