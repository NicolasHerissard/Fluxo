namespace Finance.Android17.Domain;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAtUtc { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();

    /// <summary>À appeler à chaque modification : date la mise à jour et renouvelle <see cref="Version"/>.</summary>
    public void Touch(DateTime nowUtc)
    {
        UpdatedAtUtc = nowUtc;
        Version = Guid.NewGuid();
    }

    /// <summary>Suppression logique : l'entité reste en base mais n'est plus proposée.</summary>
    public void MarkDeleted(DateTime nowUtc)
    {
        DeletedAtUtc = nowUtc;
        Touch(nowUtc);
    }

    public void MarkRestored(DateTime nowUtc)
    {
        DeletedAtUtc = null;
        Touch(nowUtc);
    }
}
