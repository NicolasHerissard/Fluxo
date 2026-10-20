using Finance.Android17.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Android17.Infrastructure;

/// <summary>Écritures communes à tous les dépôts d'entités : ajout, modification et suppression logique datés par l'horloge.</summary>
internal abstract class Repository<T>(FinanceDatabase database) where T : Entity
{
    protected FinanceDatabase Database { get; } = database;

    protected abstract DbSet<T> Set(FinanceDbContext db);

    protected async Task InsertAsync(Func<DateTime, T> create, CancellationToken cancellationToken)
    {
        await using var db = await Database.OpenAsync(cancellationToken).ConfigureAwait(false);
        Set(db).Add(create(Database.UtcNow));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Modifie une entité non supprimée ; ne fait rien si elle n'existe pas ou a été supprimée.</summary>
    protected Task ChangeAsync(Guid id, Action<T> change, CancellationToken cancellationToken) =>
        ModifyAsync(id, includeDeleted: false, (entity, now) => { change(entity); entity.Touch(now); }, cancellationToken);

    protected Task MarkDeletedAsync(Guid id, CancellationToken cancellationToken) =>
        ModifyAsync(id, includeDeleted: true, (entity, now) => entity.MarkDeleted(now), cancellationToken);

    protected Task MarkRestoredAsync(Guid id, CancellationToken cancellationToken) =>
        ModifyAsync(id, includeDeleted: true, (entity, now) => entity.MarkRestored(now), cancellationToken);

    private async Task ModifyAsync(Guid id, bool includeDeleted, Action<T, DateTime> apply, CancellationToken cancellationToken)
    {
        await using var db = await Database.OpenAsync(cancellationToken).ConfigureAwait(false);
        var entity = await Set(db).FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (entity is null || (!includeDeleted && entity.DeletedAtUtc is not null)) return;
        apply(entity, Database.UtcNow);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
