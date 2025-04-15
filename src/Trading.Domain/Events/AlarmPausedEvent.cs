using MediatR;

namespace Trading.Domain.Events;

public class AlarmPausedEvent : INotification
{
    public string AlarmId { get; }

    public AlarmPausedEvent(string alarmId)
    {
        AlarmId = alarmId;
    }
}
