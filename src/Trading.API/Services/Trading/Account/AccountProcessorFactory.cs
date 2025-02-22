using Trading.Domain.Entities;

namespace Trading.API.Services.Trading.Account;

public class AccountProcessorFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<AccountType, Type> _handlers;

    public AccountProcessorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _handlers = new Dictionary<AccountType, Type>
        {
            {AccountType.Spot, typeof(SpotProcessor)},
            {AccountType.Feature, typeof(FeatureProcessor)},
        };
    }

    public virtual IAccountProcessor? GetAccountProcessor(AccountType type)
    {
        return _handlers.TryGetValue(type, out var handlerType)
            ? _serviceProvider.GetService(handlerType) as IAccountProcessor
            : null;
    }
}