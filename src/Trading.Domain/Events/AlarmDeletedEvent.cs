using MediatR;

namespace Trading.Domain.Events;

public class AlarmDeletedEvent : INotification
{
    public string AlarmId { get; }

    public AlarmDeletedEvent(string alarmId)
    {
        AlarmId = alarmId;
    }
}
