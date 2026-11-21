using System.Net.Http.Headers;
using System.Security.Cryptography;
using Finance.Android17.Application;

namespace Finance.Android17.Maui;

/// <summary>
/// Mise à jour intégrée : lit les releases GitHub de Fluxo, télécharge l'APK, vérifie sa taille et son empreinte, puis ouvre
/// l'installateur du système (Android demande toujours sa propre confirmation). Appelée uniquement sur action de l'utilisateur.
/// </summary>
public sealed class AppUpdateService : IAppUpdateService
{
    private const string ReleasesUrl = "https://api.github.com/repos/NicolasHerissard/Fluxo/releases?per_page=30";
    internal const string UpdatesDirectoryName = "updates";

    private static readonly HttpClient Http = CreateClient();

    public long CurrentVersionCode => long.TryParse(AppInfo.Current.BuildString, out var code) ? code : 0;
    public string CurrentVersionName => AppInfo.Current.VersionString;

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Fluxo", AppInfo.Current.VersionString));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public async Task<AppUpdateInfo?> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var json = await Http.GetStringAsync(ReleasesUrl, timeout.Token);
            return AppUpdateParser.FindNewer(json, CurrentVersionCode);
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Impossible de joindre le serveur de mises à jour. Vérifiez votre connexion Internet.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("Le serveur de mises à jour ne répond pas. Réessayez plus tard.");
        }
    }

    public async Task InstallAsync(AppUpdateInfo update, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!AppUpdateParser.IsTrustedUrl(update.ApkUrl))
            throw new InvalidOperationException("Lien de téléchargement non reconnu : mise à jour annulée.");
#if ANDROID
        var directory = Path.Combine(FileSystem.CacheDirectory, UpdatesDirectoryName);
        try
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            Directory.CreateDirectory(directory);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"fluxo-{update.VersionCode}.apk");

        try
        {
            using var response = await Http.GetAsync(update.ApkUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? update.SizeBytes;
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var target = File.Create(path))
            {
                var buffer = new byte[81920];
                long read = 0;
                int count;
                while ((count = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                    read += count;
                    if (total > 0) progress?.Report(Math.Min(1, (double)read / total));
                }
            }
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Téléchargement impossible. Vérifiez votre connexion Internet et réessayez.");
        }

        if (update.Sha256 is null || update.SizeBytes <= 0)
        {
            File.Delete(path);
            throw new InvalidOperationException("La version proposée n’indique pas son empreinte ou sa taille : impossible de vérifier le fichier. Rien n’a été installé.");
        }

        var info = new FileInfo(path);
        if (info.Length != update.SizeBytes)
            throw new InvalidOperationException("Le fichier téléchargé est incomplet. Réessayez.");
        await using (var stream = File.OpenRead(path))
        {
            var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
            if (hash != update.Sha256)
            {
                stream.Close();
                File.Delete(path);
                throw new InvalidOperationException("Le fichier téléchargé est corrompu (empreinte différente). Rien n’a été installé.");
            }
        }

        var context = Platform.AppContext;
        if (!context.PackageManager!.CanRequestPackageInstalls())
        {
            // Première fois seulement : Android demande d'autoriser Fluxo à installer des applications.
            var settings = new global::Android.Content.Intent(global::Android.Provider.Settings.ActionManageUnknownAppSources,
                global::Android.Net.Uri.Parse($"package:{context.PackageName}"));
            settings.AddFlags(global::Android.Content.ActivityFlags.NewTask);
            context.StartActivity(settings);
            throw new InvalidOperationException("Autorisez Fluxo à installer des applications dans la fenêtre qui s’ouvre, puis revenez et relancez l’installation.");
        }

        var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(context, $"{context.PackageName}.fileprovider", new Java.IO.File(path))!;
        var install = new global::Android.Content.Intent(global::Android.Content.Intent.ActionView);
        install.SetDataAndType(uri, "application/vnd.android.package-archive");
        install.AddFlags(global::Android.Content.ActivityFlags.GrantReadUriPermission | global::Android.Content.ActivityFlags.NewTask);
        context.StartActivity(install);
#else
        await Task.CompletedTask;
        throw new InvalidOperationException("La mise à jour intégrée n’est disponible que sur Android.");
#endif
    }
}
