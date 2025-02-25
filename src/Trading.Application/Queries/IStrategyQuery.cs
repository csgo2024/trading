using Trading.Common.Models;
using Trading.Domain.Entities;

namespace Trading.Application.Queries;

public interface IStrategyQuery
{
    Task<PagedResult<Strategy>> GetStrategyListAsync(PagedRequest pagedRequest, CancellationToken cancellationToken = default);
    Task<Strategy?> GetStrategyByIdAsync(string id, CancellationToken cancellationToken = default);

}
