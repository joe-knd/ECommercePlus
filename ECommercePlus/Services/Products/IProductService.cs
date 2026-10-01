using ECommercePlus.Domain;

namespace ECommercePlus.Services.Products;

public interface IProductService
{
    Task<PagedResult<Product>> SearchAsync(ProductSearchQuery query, CancellationToken cancellationToken = default);
    Task<Product?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetManyAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<OperationResult<Product>> CreateAsync(ProductInput input, CancellationToken cancellationToken = default);
    Task<OperationResult<Product>> UpdateAsync(int id, ProductInput input, Guid expectedVersion, CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
