using MediatR;
using Trading.Domain.Entities;

namespace Trading.Domain.Events;

public class AlertPausedEvent : INotification
{
    public string AlertId { get; }

    public AlertPausedEvent(string alertId)
    {
        AlertId = alertId;
    }
}
public class AlertDeletedEvent : INotification
{
    public string AlertId { get; }

    public AlertDeletedEvent(string alertId)
    {
        AlertId = alertId;
    }
}
public class AlertCreatedEvent : INotification
{
    public Alert Alert { get; }
    public AlertCreatedEvent(Alert alert)
    {
        Alert = alert;
    }
}
public class AlertEmptyedEvent : INotification
{

    public AlertEmptyedEvent()
    {
    }
}
public class AlertResumedEvent : INotification
{
    public Alert Alert { get; }

    public AlertResumedEvent(Alert alert)
    {
        Alert = alert;
    }
}
