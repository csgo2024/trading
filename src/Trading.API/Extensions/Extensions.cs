using Trading.API.Services.Trading.Account;
using Trading.API.Services.Trading.Executors;

namespace Trading.API.Extensions;

public static class Extensions
{
    public static IServiceCollection AddTradingServices(this IServiceCollection services)
    {
        services.AddSingleton<FeatureProcessor>();
        services.AddSingleton<SpotProcessor>();
        services.AddSingleton<AccountProcessorFactory>();
        
        
        services.AddSingleton<BottomBuyExecutor>();
        services.AddSingleton<DCABuyExecutor>();
        services.AddSingleton<ExecutorFactory>();
        return services;
    }
}