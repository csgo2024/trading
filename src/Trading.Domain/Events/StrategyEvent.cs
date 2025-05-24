using MediatR;
using Trading.Domain.Entities;

namespace Trading.Domain.Events;

public record StrategyCreatedEvent(Strategy Strategy) : INotification;
public record StrategyDeletedEvent(Strategy Strategy) : INotification;
public record StrategyPausedEvent(Strategy Strategy) : INotification;
public record StrategyResumedEvent(Strategy Strategy) : INotification;
