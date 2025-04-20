using MediatR;

namespace Trading.Domain.Events;

public class AlarmEmptyedEvent : INotification
{

    public AlarmEmptyedEvent()
    {
    }
}
