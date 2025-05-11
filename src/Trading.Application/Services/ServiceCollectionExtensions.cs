using Microsoft.Extensions.DependencyInjection;
using Trading.Application.JavaScript;
using Trading.Application.Services.Alerts;
using Trading.Application.Services.Common;
using Trading.Application.Services.Trading;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Services.Trading.Executors;

namespace Trading.Application.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTradingServices(this IServiceCollection services)
    {
        services.AddSingleton<AlertNotificationService>();
        services.AddSingleton<BottomBuyExecutor>();
        services.AddSingleton<DCABuyExecutor>();
        services.AddSingleton<CloseSellExecutor>();
        services.AddSingleton<CloseBuyExecutor>();
        services.AddSingleton<TopSellExecutor>();
        services.AddSingleton<FutureProcessor>();
        services.AddSingleton<IAccountProcessorFactory, AccountProcessorFactory>();
        services.AddSingleton<IBackgroundTaskManager, BackgroundTaskManager>();
        services.AddSingleton<IExecutorFactory, ExecutorFactory>();
        services.AddSingleton<IKlineStreamManager, KlineStreamManager>();
        services.AddSingleton<IStrategyStateManager, StrategyStateManager>();
        services.AddSingleton<JavaScriptEvaluator>();
        services.AddSingleton<SpotProcessor>();
        services.AddSingleton<StrategyDispatchService>();
        return services;
    }

}
