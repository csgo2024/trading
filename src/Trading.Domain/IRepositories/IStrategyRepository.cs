using Trading.Common.Enums;
using Trading.Domain.Entities;

namespace Trading.Domain.IRepositories;

public interface IStrategyRepository : IRepository<Strategy>
{
    Task<Strategy?> Add(Strategy entity, CancellationToken cancellationToken = default);
    Task<Dictionary<string, Strategy>> FindActiveStrategies();
    Task<List<Strategy>> GetAllStrategies();
    Task<List<Strategy>> FindActiveStrategyByType(StrategyType strategyType,
                                                  CancellationToken cancellationToken = default);
}
