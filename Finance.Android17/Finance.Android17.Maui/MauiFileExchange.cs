using Finance.Android17.Application;

namespace Finance.Android17.Maui;

/// <summary>Partage de fichiers par la feuille de partage native et sélection par le sélecteur de fichier du système.</summary>
public sealed class MauiFileExchange : IFileExchange
{
    public async Task ShareAsync(string fileName, byte[] content, string title, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, fileName);
        await File.WriteAllBytesAsync(path, content, cancellationToken);
        await Share.Default.RequestAsync(new ShareFileRequest(title, new ShareFile(path)));
    }

    public async Task<byte[]?> PickAsync(string title, CancellationToken cancellationToken = default)
    {
        var selected = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = title });
        if (selected is null) return null;
        await using var source = await selected.OpenReadAsync();
        using var memory = new MemoryStream();
        await source.CopyToAsync(memory, cancellationToken);
        return memory.ToArray();
    }
}
