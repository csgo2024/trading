using Trading.Domain.Entities;

namespace Trading.Domain.IRepositories;

public interface IStrategyRepository : IRepository<Strategy>
{
    Task<Strategy?> Add(Strategy entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateOrderStatusAsync(Strategy entity, CancellationToken cancellationToken = default);

    Task<bool> UpdateStatusAsync(StateStatus newStatus);

    Task<Dictionary<string, Strategy>?> InitializeSpotStrategies();
    Task<Dictionary<string, Strategy>?> InitializeFutureStrategies();

    Task<Dictionary<string, Strategy>> InitializeActiveStrategies();

    /// <summary>
    /// get all strategies .
    /// </summary>
    /// <returns></returns>
    Task<List<Strategy>> GetAllStrategies();

}
