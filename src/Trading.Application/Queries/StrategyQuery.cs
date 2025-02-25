using MongoDB.Driver;
using Trading.Common.Models;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Queries;

public class StrategyQuery : IStrategyQuery
{
    private readonly IStrategyRepository _strategyRepository;
    public StrategyQuery(IStrategyRepository strategyRepository)
    {
        _strategyRepository = strategyRepository;
    }
    public async Task<PagedResult<Strategy>> GetStrategyListAsync(PagedRequest pagedRequest, CancellationToken cancellationToken = default)
    {
        pagedRequest.Sort = Builders<Strategy>.Sort.Ascending(x => x.Symbol);
        return await _strategyRepository.GetPagedResultAsync(pagedRequest, cancellationToken);
    }

    public async Task<Strategy?> GetStrategyByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _strategyRepository.GetByIdAsync(id, cancellationToken);
    }
}
