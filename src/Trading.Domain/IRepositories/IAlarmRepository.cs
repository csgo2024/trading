using Trading.Domain.Entities;

namespace Trading.Domain.IRepositories;

public interface IAlarmRepository : IRepository<Alarm>
{
    Task<IEnumerable<Alarm>> GetActiveAlarmsAsync(CancellationToken cancellationToken);

    public IEnumerable<Alarm> GetActiveAlarms(string symbol);

    Task<bool> DeactivateAlarmAsync(string alarm, CancellationToken cancellationToken);
}
