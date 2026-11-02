namespace Finance.Android17.Application;

/// <summary>Port de plateforme : montre des alertes du centre de notifications comme notifications du système.</summary>
public interface IAlertPublisher
{
    /// <summary>
    /// Affiche une notification par alerte. Renvoie <c>false</c> quand la plateforme refuse (autorisation absente) :
    /// rien n'a été montré, et l'appelant retentera au prochain contrôle.
    /// </summary>
    Task<bool> PublishAsync(IReadOnlyList<AppNotification> alerts, CancellationToken cancellationToken = default);
}
