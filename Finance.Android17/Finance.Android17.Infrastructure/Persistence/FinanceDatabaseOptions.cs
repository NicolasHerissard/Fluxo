namespace Finance.Android17.Infrastructure;

/// <summary>Emplacement du fichier de base SQLCipher.</summary>
public sealed record FinanceDatabaseOptions(string DatabasePath)
{
    /// <summary>Nom historique du fichier : ne pas le changer sans stratégie de migration.</summary>
    public const string DefaultFileName = "finance-v2.db3";

    /// <summary>Base de l'application : <c>finance-v2.db3</c> dans le dossier de données locales de l'utilisateur.</summary>
    public static FinanceDatabaseOptions Default() =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DefaultFileName));
}
