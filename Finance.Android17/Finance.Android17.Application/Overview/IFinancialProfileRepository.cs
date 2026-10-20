namespace Finance.Android17.Application;

/// <summary>Le solde de départ saisi par l'utilisateur : le point de départ du solde actuel.</summary>
public interface IFinancialProfileRepository
{
    Task<long> GetInitialBalanceAsync(CancellationToken cancellationToken = default);
    Task SetInitialBalanceAsync(long amountMinor, CancellationToken cancellationToken = default);
}
