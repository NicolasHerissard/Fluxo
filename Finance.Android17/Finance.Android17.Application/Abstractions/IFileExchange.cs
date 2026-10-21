namespace Finance.Android17.Application;

/// <summary>Échange de fichiers avec l'utilisateur : feuille de partage du système et sélecteur de fichier.</summary>
public interface IFileExchange
{
    /// <summary>Écrit le contenu dans un fichier temporaire nommé <paramref name="fileName"/> puis ouvre la feuille de partage.</summary>
    Task ShareAsync(string fileName, byte[] content, string title, CancellationToken cancellationToken = default);

    /// <summary>Laisse l'utilisateur choisir un fichier ; renvoie <see langword="null"/> s'il annule.</summary>
    Task<byte[]?> PickAsync(string title, CancellationToken cancellationToken = default);
}
