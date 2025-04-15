using MediatR;

namespace Trading.Domain.Events;

public class AlarmEmptyEvent : INotification
{

    public AlarmEmptyEvent()
    {
    }
}
