using Finance.Android17.Application;
using Finance.Android17.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Android17.Infrastructure;

internal sealed class FinancialProfileRepository(FinanceDatabase database) : IFinancialProfileRepository
{
    public async Task<long> GetInitialBalanceAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        return (await db.FinancialProfiles.AsNoTracking().SingleAsync(x => x.Id == FinancialProfile.SingletonId, cancellationToken).ConfigureAwait(false)).InitialBalanceMinor;
    }

    public async Task SetInitialBalanceAsync(long amountMinor, CancellationToken cancellationToken = default)
    {
        await using var db = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        var profile = await db.FinancialProfiles.SingleAsync(x => x.Id == FinancialProfile.SingletonId, cancellationToken).ConfigureAwait(false);
        profile.InitialBalanceMinor = amountMinor;
        profile.UpdatedAtUtc = database.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
