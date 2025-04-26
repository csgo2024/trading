using MediatR;
using Microsoft.Extensions.Logging;
using Trading.Application.Helpers;
using Trading.Application.Services.Alerts;
using Trading.Application.Services.Trading.Account;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Services.Trading.Executors;

public class CloseBuyExecutor : BaseExecutor,
    INotificationHandler<KlineClosedEvent>
{
    private readonly IStrategyRepository _strategyRepository;
    private readonly IAccountProcessorFactory _accountProcessorFactory;

    public CloseBuyExecutor(ILogger<CloseBuyExecutor> logger,
                            IAccountProcessorFactory accountProcessorFactory,
                            IStrategyRepository strategyRepository) : base(logger)
    {
        _accountProcessorFactory = accountProcessorFactory;
        _strategyRepository = strategyRepository;
    }

    public override Task Execute(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public async Task Handle(KlineClosedEvent notification, CancellationToken cancellationToken)
    {
        var strategies = await _strategyRepository.Find(notification.Symbol,
                                                 CommonHelper.ConvertToIntervalString(notification.Interval),
                                                 StrategyType.CloseBuy,
                                                 cancellationToken);
        foreach (var strategy in strategies)
        {
            var accountProcessor = _accountProcessorFactory.GetAccountProcessor(strategy.AccountType);
            if (accountProcessor != null)
            {
                var filterData = await accountProcessor.GetSymbolFilterData(strategy, cancellationToken);
                var closePrice = notification.Kline.ClosePrice;
                strategy.TargetPrice = CommonHelper.AdjustPriceByStepSize(closePrice * (1 - strategy.Volatility), filterData.Item1);
                strategy.Quantity = CommonHelper.AdjustQuantityBystepSize(strategy.Amount / strategy.TargetPrice, filterData.Item2);
                if (!strategy.HasOpenOrder)
                {
                    await TryPlaceOrder(accountProcessor, strategy, cancellationToken);
                }
                strategy.UpdatedAt = DateTime.UtcNow;
                await _strategyRepository.UpdateAsync(strategy.Id, strategy, cancellationToken);
            }
        }
    }
}
