using MediatR;

namespace Trading.Domain.Events;

public class PriceAlertCreatedEvent : INotification
{

    public PriceAlertCreatedEvent()
    {
    }
}
