using MediatR;
using Trading.Domain.Entities;

namespace Trading.Domain.Events;

public class AlarmResumedEvent : INotification
{
    public Alarm Alarm { get; }

    public AlarmResumedEvent(Alarm alarm)
    {
        Alarm = alarm;
    }
}
