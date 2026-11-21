using Finance.Android17.Application;

namespace Finance.Android17.Tests.Updates;

public sealed class AppUpdateParserTests
{
    private static string Release(string name, string asset, string url = "https://github.com/NicolasHerissard/Fluxo/releases/download/t/x.apk", bool draft = false, string extra = "") =>
        $$"""{"name":"{{name}}","tag_name":"t","draft":{{(draft ? "true" : "false")}},"body":"notes","assets":[{"name":"{{asset}}","browser_download_url":"{{url}}","size":1234{{extra}}}]}""";

    [Fact]
    public void FindNewer_ReturnsTheHighestVersionAboveTheCurrentOne()
    {
        var json = $"[{Release("APK #110", "fluxo-110.apk")},{Release("APK #112", "fluxo-112.apk")},{Release("APK #108", "fluxo-108.apk")}]";

        var update = AppUpdateParser.FindNewer(json, 109);

        Assert.NotNull(update);
        Assert.Equal(112, update.VersionCode);
        Assert.Equal("APK #112", update.VersionName);
        Assert.Equal(1234, update.SizeBytes);
    }

    [Fact]
    public void FindNewer_ReturnsNullWhenUpToDate() =>
        Assert.Null(AppUpdateParser.FindNewer($"[{Release("APK #110", "fluxo-110.apk")}]", 110));

    [Theory]
    [InlineData("fluxo-test-120.apk")]
    [InlineData("fluxo.apk")]
    [InlineData("autre-120.apk")]
    public void FindNewer_IgnoresAssetsThatAreNotProductionApks(string asset) =>
        Assert.Null(AppUpdateParser.FindNewer($"[{Release("x", asset)}]", 1));

    [Fact]
    public void FindNewer_IgnoresDraftsAndUntrustedLinks()
    {
        Assert.Null(AppUpdateParser.FindNewer($"[{Release("x", "fluxo-200.apk", draft: true)}]", 1));
        Assert.Null(AppUpdateParser.FindNewer($"[{Release("x", "fluxo-200.apk", url: "http://github.com/a.apk")}]", 1));
        Assert.Null(AppUpdateParser.FindNewer($"[{Release("x", "fluxo-200.apk", url: "https://exemple.org/a.apk")}]", 1));
    }

    [Fact]
    public void FindNewer_ReadsTheSha256Digest()
    {
        var update = AppUpdateParser.FindNewer($"[{Release("x", "fluxo-200.apk", extra: ",\"digest\":\"sha256:ABCDEF\"")}]", 1);

        Assert.Equal("abcdef", update?.Sha256);
    }

    [Theory]
    [InlineData("pas du json")]
    [InlineData("{\"message\":\"Not Found\"}")]
    public void FindNewer_RejectsUnexpectedResponses(string json) =>
        Assert.Throws<InvalidOperationException>(() => AppUpdateParser.FindNewer(json, 1));
}
