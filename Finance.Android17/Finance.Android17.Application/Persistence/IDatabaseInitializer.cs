namespace Finance.Android17.Application;

public interface IDatabaseInitializer
{
    /// <summary>Ouvre la base, crée ou met à jour son schéma et ajoute les catégories par défaut si elle est neuve. Sans effet si c'est déjà fait.</summary>
    /// <exception cref="InvalidOperationException">La base a été créée par une version plus récente de l'application.</exception>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
