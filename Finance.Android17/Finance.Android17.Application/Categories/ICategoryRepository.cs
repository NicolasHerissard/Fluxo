using Finance.Android17.Domain;

namespace Finance.Android17.Application;

public interface ICategoryRepository
{
    /// <summary>Catégories non supprimées, par ordre alphabétique.</summary>
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(string name, string color, long? budgetMinor = null, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, string name, string color, long? budgetMinor, CancellationToken cancellationToken = default);

    /// <exception cref="InvalidOperationException">Une dépense non supprimée utilise encore la catégorie.</exception>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
