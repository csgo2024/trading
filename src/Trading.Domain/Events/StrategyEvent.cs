using MediatR;
using Trading.Domain.Entities;

namespace Trading.Domain.Events;

public record StrategyCreatedEvent(Strategy Strategy) : INotification;
public record StrategyDeletedEvent(string Id) : INotification;
public record StrategyPausedEvent(string Id) : INotification;
public record StrategyResumedEvent(Strategy Strategy) : INotification;
