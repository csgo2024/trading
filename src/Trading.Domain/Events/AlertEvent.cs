using MediatR;
using Trading.Domain.Entities;

namespace Trading.Domain.Events;

public record AlertCreatedEvent(Alert Alert) : INotification;
public record AlertDeletedEvent(Alert Alert) : INotification;
public record AlertPausedEvent(Alert Alert) : INotification;
public record AlertResumedEvent(Alert Alert) : INotification;
public record AlertEmptyedEvent() : INotification;
