using Finance.Android17.Application;
using Finance.Android17.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Android17.Infrastructure;

internal sealed class SubscriptionRepository(FinanceDatabase database) : Repository<Subscription>(database), ISubscriptionRepository
{
    protected override DbSet<Subscription> Set(FinanceDbContext db) => db.Subscriptions;

    public async Task<IReadOnlyList<Subscription>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await Database.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await db.Subscriptions.AsNoTracking().Where(x => x.DeletedAtUtc == null).OrderBy(x => x.NextBillingDate)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task AddAsync(string name, long amountMinor, int billingIntervalMonths, DateTime nextBillingDate, string currencyCode = "EUR", CancellationToken cancellationToken = default) =>
        InsertAsync(now => new Subscription
        {
            Name = name.Trim(),
            AmountMinor = amountMinor,
            CurrencyCode = currencyCode,
            BillingIntervalMonths = Math.Max(1, billingIntervalMonths),
            NextBillingDate = nextBillingDate.Date,
            AnchorDay = nextBillingDate.Day,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }, cancellationToken);

    public Task UpdateAsync(Guid id, string name, long amountMinor, int billingIntervalMonths, DateTime nextBillingDate, string currencyCode = "EUR", CancellationToken cancellationToken = default) =>
        ChangeAsync(id, subscription =>
        {
            subscription.Name = name.Trim();
            subscription.AmountMinor = amountMinor;
            subscription.CurrencyCode = currencyCode;
            subscription.BillingIntervalMonths = Math.Max(1, billingIntervalMonths);
            // Une date inchangée garde son jour d'ancrage (le 31 affiché « 28 février ») ; une date choisie le redéfinit.
            if (subscription.NextBillingDate != nextBillingDate.Date || subscription.AnchorDay <= 0)
                subscription.AnchorDay = nextBillingDate.Day;
            subscription.NextBillingDate = nextBillingDate.Date;
        }, cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => MarkDeletedAsync(id, cancellationToken);
}
