using Finance.Android17.Application;

namespace Finance.Android17.Maui;

/// <summary>Partage de fichiers par la feuille de partage native et sélection par le sélecteur de fichier du système.</summary>
public sealed class MauiFileExchange : IFileExchange
{
    /// <summary>Taille maximale d'un fichier à restaurer : une sauvegarde réelle pèse quelques centaines de ko.</summary>
    internal const long MaxPickedBytes = 50L * 1024 * 1024;

    // Dossier dédié : tout ce qu'il contient est un export déjà partagé, à effacer sans condition.
    private static string ExportDirectory => Path.Combine(FileSystem.CacheDirectory, "exports");

    public async Task ShareAsync(string fileName, byte[] content, string title, CancellationToken cancellationToken = default)
    {
        PurgeExports();
        Directory.CreateDirectory(ExportDirectory);
        var path = Path.Combine(ExportDirectory, Path.GetFileName(fileName));
        await File.WriteAllBytesAsync(path, content, cancellationToken);
        await Share.Default.RequestAsync(new ShareFileRequest(title, new ShareFile(path)));
    }

    /// <summary>
    /// Efface les CSV et sauvegardes laissés dans le cache par un partage précédent. Le fichier doit rester lisible tant
    /// que l'application de destination le lit : on purge donc au prochain partage, au démarrage et au verrouillage.
    /// </summary>
    public static void PurgeExports()
    {
        try
        {
            if (Directory.Exists(ExportDirectory)) Directory.Delete(ExportDirectory, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public async Task<byte[]?> PickAsync(string title, CancellationToken cancellationToken = default)
    {
        var selected = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = title });
        if (selected is null) return null;
        await using var source = await selected.OpenReadAsync();
        if (source.CanSeek && source.Length > MaxPickedBytes) throw new InvalidOperationException("Ce fichier est trop volumineux pour être une sauvegarde.");
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (memory.Length + read > MaxPickedBytes) throw new InvalidOperationException("Ce fichier est trop volumineux pour être une sauvegarde.");
            memory.Write(buffer, 0, read);
        }
        return memory.ToArray();
    }
}
