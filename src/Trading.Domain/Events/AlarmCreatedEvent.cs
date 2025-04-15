using MediatR;
using Trading.Domain.Entities;

namespace Trading.Domain.Events;

public class AlarmCreatedEvent : INotification
{
    public Alarm Alarm { get; }
    public AlarmCreatedEvent(Alarm alarm)
    {
        Alarm = alarm;
    }
}
