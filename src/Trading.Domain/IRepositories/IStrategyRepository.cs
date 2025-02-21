using Trading.Domain.Entities;

namespace Trading.Domain.IRepositories;

public interface IStrategyRepository: IRepository<Strategy>
{
    Task<Strategy?> Add(Strategy entity);
    Task<bool> UpdateOrderStatusAsync(Strategy entity, CancellationToken cancellationToken = default);

    Task<bool> UpdateStatusAsync(StrateStatus newStatus);


    Task<Dictionary<string, Strategy>?> InitializeSpotStrategies();
    Task<Dictionary<string, Strategy>?> InitializeFeatureStrategies();
    

    /// <summary>
    /// get all strategies .
    /// </summary>
    /// <returns></returns>
    Task<List<Strategy>> GetAllStrategies();
 
}