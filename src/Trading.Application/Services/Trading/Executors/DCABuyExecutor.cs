using Microsoft.Extensions.Logging;
using Trading.Application.JavaScript;
using Trading.Application.Services.Alerts;
using Trading.Application.Services.Trading.Account;
using Trading.Common.Enums;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Services.Trading.Executors;

public class DCABuyExecutor : BaseExecutor
{
    public DCABuyExecutor(ILogger<BaseExecutor> logger,
                          IStrategyRepository strategyRepository,
                          JavaScriptEvaluator javaScriptEvaluator,
                          IStrategyStateManager strategyStateManager)
        : base(logger, strategyRepository, javaScriptEvaluator, strategyStateManager)
    {
    }

    public override StrategyType StrategyType => StrategyType.DCA;
    public override Task ExecuteAsync(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public override Task Handle(KlineClosedEvent notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
