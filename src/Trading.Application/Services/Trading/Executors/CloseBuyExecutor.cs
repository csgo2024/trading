using MediatR;
using Microsoft.Extensions.Logging;
using Trading.Application.Services.Alerts;
using Trading.Application.Services.Trading.Account;
using Trading.Common.Enums;
using Trading.Domain.IRepositories;
using Trading.Exchange.Binance.Helpers;

namespace Trading.Application.Services.Trading.Executors;

public class CloseBuyExecutor : BaseExecutor,
    INotificationHandler<KlineClosedEvent>
{
    private readonly IAccountProcessorFactory _accountProcessorFactory;

    public CloseBuyExecutor(ILogger<CloseBuyExecutor> logger,
                            IAccountProcessorFactory accountProcessorFactory,
                            IStrategyRepository strategyRepository) : base(logger, strategyRepository)
    {
        _accountProcessorFactory = accountProcessorFactory;
    }

    public async Task Handle(KlineClosedEvent notification, CancellationToken cancellationToken)
    {
        var strategies = await _strategyRepository.Find(notification.Symbol,
                                                        BinanceHelper.ConvertToIntervalString(notification.Interval),
                                                        StrategyType.CloseBuy,
                                                        cancellationToken);
        foreach (var strategy in strategies)
        {
            var accountProcessor = _accountProcessorFactory.GetAccountProcessor(strategy.AccountType);
            if (accountProcessor != null)
            {
                var filterData = await accountProcessor.GetSymbolFilterData(strategy, cancellationToken);
                var closePrice = notification.Kline.ClosePrice;
                strategy.TargetPrice = BinanceHelper.AdjustPriceByStepSize(closePrice * (1 - strategy.Volatility), filterData.Item1);
                strategy.Quantity = BinanceHelper.AdjustQuantityBystepSize(strategy.Amount / strategy.TargetPrice, filterData.Item2);
                strategy.UpdatedAt = DateTime.UtcNow;
                await TryPlaceOrder(accountProcessor, strategy, cancellationToken);
                await _strategyRepository.UpdateAsync(strategy.Id, strategy, cancellationToken);
            }
        }
    }
}
