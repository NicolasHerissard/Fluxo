namespace Finance.Android17.Application;

public interface IAppLockService
{
    Task<bool> HasPinAsync();
    Task SetPinAsync(string pin, CancellationToken cancellationToken = default);

    /// <summary>Vérifie le PIN. Renvoie toujours <c>false</c>, sans le comparer, tant que le verrouillage après échecs est actif.</summary>
    Task<bool> VerifyPinAsync(string pin, CancellationToken cancellationToken = default);

    /// <summary>Temps restant avant que de nouveaux essais soient acceptés (zéro : essais possibles).</summary>
    Task<TimeSpan> GetLockoutRemainingAsync(CancellationToken cancellationToken = default);
}
