using System.Text.Json;
using System.Text.RegularExpressions;

namespace Finance.Android17.Application;

/// <summary>Version plus récente publiée par la CI : numéro de build Android, APK à télécharger et son empreinte.</summary>
public sealed record AppUpdateInfo(long VersionCode, string VersionName, string ApkUrl, long SizeBytes, string? Sha256, string Notes);

/// <summary>
/// Port de la mise à jour intégrée. L'adaptateur plateforme interroge les releases GitHub, télécharge l'APK et lance
/// l'installateur du système : rien ne part sans action explicite de l'utilisateur (aucune vérification en arrière-plan).
/// </summary>
public interface IAppUpdateService
{
    long CurrentVersionCode { get; }
    string CurrentVersionName { get; }

    /// <summary>Renvoie la mise à jour disponible, ou <c>null</c> si l'application est à jour. Lève <see cref="InvalidOperationException"/> (message en français) en cas d'échec.</summary>
    Task<AppUpdateInfo?> CheckAsync(CancellationToken cancellationToken = default);

    /// <summary>Télécharge et vérifie l'APK puis ouvre l'installateur du système. Lève <see cref="InvalidOperationException"/> (message en français) en cas d'échec.</summary>
    Task InstallAsync(AppUpdateInfo update, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}

/// <summary>Lecture de la liste des releases GitHub (réponse JSON de <c>/repos/{owner}/{repo}/releases</c>).</summary>
public static partial class AppUpdateParser
{
    /// <summary>Seuls les APK signés en production portent ce nom : un APK de test (clé jetable) ne pourrait pas remplacer l'application installée.</summary>
    [GeneratedRegex(@"^fluxo-(\d+)\.apk$")]
    private static partial Regex ApkNamePattern();

    private static readonly string[] AllowedHosts = ["github.com"];

    /// <summary>Plus récente release strictement supérieure à <paramref name="currentVersionCode"/>, ou <c>null</c>.</summary>
    public static AppUpdateInfo? FindNewer(string releasesJson, long currentVersionCode)
    {
        JsonDocument document;
        try { document = JsonDocument.Parse(releasesJson); }
        catch (JsonException) { throw new InvalidOperationException("Réponse du serveur de mises à jour illisible."); }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Réponse du serveur de mises à jour inattendue.");

            AppUpdateInfo? best = null;
            foreach (var release in document.RootElement.EnumerateArray())
            {
                if (release.ValueKind != JsonValueKind.Object || Flag(release, "draft")) continue;
                if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) continue;

                foreach (var asset in assets.EnumerateArray())
                {
                    var match = ApkNamePattern().Match(Text(asset, "name"));
                    if (!match.Success || !long.TryParse(match.Groups[1].Value, out var code)) continue;
                    if (code <= currentVersionCode || (best is not null && code <= best.VersionCode)) continue;

                    var url = Text(asset, "browser_download_url");
                    if (!IsTrustedUrl(url)) continue;

                    var digest = Text(asset, "digest");
                    var sha = digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? digest[7..].ToLowerInvariant() : null;
                    var name = Text(release, "name");
                    best = new AppUpdateInfo(
                        code,
                        name.Length > 0 ? name : Text(release, "tag_name"),
                        url,
                        asset.TryGetProperty("size", out var size) && size.TryGetInt64(out var bytes) ? bytes : 0,
                        sha,
                        Text(release, "body"));
                }
            }
            return best;
        }
    }

    /// <summary>Lien de téléchargement accepté : HTTPS et hôte GitHub uniquement.</summary>
    public static bool IsTrustedUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);

    private static string Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static bool Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
