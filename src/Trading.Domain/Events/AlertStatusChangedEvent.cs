using MediatR;

namespace Trading.Domain.Events;

public class AlertStatusChangedEvent : INotification
{
    public string AlertId { get; }
    public bool IsActive { get; }

    public AlertStatusChangedEvent(string alertId, bool isActive)
    {
        AlertId = alertId;
        IsActive = isActive;
    }
}
