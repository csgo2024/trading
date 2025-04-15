using Trading.Domain.Entities;

namespace Trading.Domain.IRepositories;

public interface IAlarmRepository : IRepository<Alarm>
{
    Task<IEnumerable<Alarm>> GetActiveAlarmsAsync(CancellationToken cancellationToken);

    public IEnumerable<Alarm> GetActiveAlarms(string symbol);

    Task<bool> DeactivateAlarmAsync(string alarm, CancellationToken cancellationToken);

    /// <summary>
    /// 清空所有告警
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>清空的告警数量</returns>
    Task<int> ClearAllAlarmsAsync(CancellationToken cancellationToken);
}
