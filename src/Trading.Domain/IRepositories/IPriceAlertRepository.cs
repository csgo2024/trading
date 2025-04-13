using Trading.Domain.Entities;

namespace Trading.Domain.IRepositories;

public interface IPriceAlertRepository : IRepository<PriceAlert>
{
    Task<IEnumerable<PriceAlert>> GetActiveAlertsAsync(CancellationToken stoppingToken);
    Task<bool> DeactivateAlertAsync(string alertId, CancellationToken stoppingToken);
}
