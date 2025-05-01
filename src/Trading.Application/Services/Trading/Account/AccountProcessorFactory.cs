using Trading.Common.Enums;

namespace Trading.Application.Services.Trading.Account;

public interface IAccountProcessorFactory
{
    IAccountProcessor? GetAccountProcessor(AccountType type);
}

public class AccountProcessorFactory : IAccountProcessorFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<AccountType, Type> _handlers;

    public AccountProcessorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _handlers = new Dictionary<AccountType, Type>
        {
            {AccountType.Spot, typeof(SpotProcessor)},
            {AccountType.Future, typeof(FutureProcessor)},
        };
    }

    public IAccountProcessor? GetAccountProcessor(AccountType type)
    {
        return _handlers.TryGetValue(type, out var handlerType)
            ? _serviceProvider.GetService(handlerType) as IAccountProcessor
            : null;
    }
}
