using Trading.Domain.Entities;

namespace Trading.Domain.IRepositories;

public interface IAlertRepository : IRepository<Alert>
{
    Task<IEnumerable<Alert>> GetActiveAlertsAsync(CancellationToken cancellationToken);

    public IEnumerable<Alert> GetActiveAlerts(string symbol);
    public IEnumerable<Alert> GetAlertsById(string[] ids);

    Task<bool> DeactivateAlertAsync(string alert, CancellationToken cancellationToken);

    Task<int> ClearAllAlertsAsync(CancellationToken cancellationToken);

    Task<List<Alert>> GetAllAlerts();

    Task<List<string>> ResumeAlertAsync(string symbol, string Interval, CancellationToken cancellationToken);
}
