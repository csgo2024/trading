using Trading.Common.Models;

namespace Trading.Domain.IRepositories;

public interface IRepository<T> where T : IEntity
{
    Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task<bool> EmptyAsync(CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(string id, T entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(T entity, CancellationToken cancellationToken = default);
    Task<PagedResult<T>> GetPagedResultAsync(PagedRequest pagedRequest, CancellationToken cancellationToken = default);

}
