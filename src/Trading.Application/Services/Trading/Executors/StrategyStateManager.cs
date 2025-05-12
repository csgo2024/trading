using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Trading.Common.Enums;
using Trading.Domain.Entities;

namespace Trading.Application.Services.Trading.Executors;

public interface IStrategyStateManager
{
    void SetState(StrategyType strategyType, Dictionary<string, Strategy> state);
    Dictionary<string, Strategy> GetState(StrategyType strategyType);
    void RemoveStrategy(Strategy strategy);
    void AddStrategy(Strategy strategy);
}

public class StrategyStateManager : IStrategyStateManager
{
    private readonly ILogger<StrategyStateManager> _logger;
    private readonly ConcurrentDictionary<StrategyType, Dictionary<string, Strategy>> _states = new();

    public StrategyStateManager(ILogger<StrategyStateManager> logger)
    {
        _logger = logger;
        _logger.LogInformation("[StrategyStateManager]HashCode: {HashCode}", GetHashCode());
    }
    public void SetState(StrategyType strategyType, Dictionary<string, Strategy> state)
    {
        _states[strategyType] = state;
    }

    public Dictionary<string, Strategy> GetState(StrategyType strategyType)
    {
        var result = _states.TryGetValue(strategyType, out var state) ? state : null;
        return result ?? [];
    }

    public void RemoveStrategy(Strategy strategy)
    {
        if (_states.TryGetValue(strategy.StrategyType, out var state) && state?.Count > 0)
        {
            if (state.Remove(strategy.Id))
            {
                // Log the removal of the strategy from the monitoring list
                _logger.LogInformation("[{AccountType}-{Symbol}-{StrategyType}] Removed strategy from monitoring list.",
                                       strategy.AccountType,
                                       strategy.Symbol,
                                       strategy.StrategyType);
            }
            if (state.Count == 0)
            {
                _states.TryRemove(strategy.StrategyType, out _);
            }
        }
    }
    public void AddStrategy(Strategy strategy)
    {
        if (_states.TryGetValue(strategy.StrategyType, out var state))
        {
            state[strategy.Id] = strategy;
        }
        else
        {
            _states[strategy.StrategyType] = new Dictionary<string, Strategy> { [strategy.Id] = strategy };
        }
        _logger.LogInformation("[{AccountType}-{Symbol}-{StrategyType}] Added strategy to monitoring list.",
                               strategy.AccountType,
                               strategy.Symbol,
                               strategy.StrategyType);
    }
}
