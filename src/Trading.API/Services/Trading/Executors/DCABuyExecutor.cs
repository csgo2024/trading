using Trading.API.Services.Trading.Account;
using Trading.Domain.Entities;

namespace Trading.API.Services.Trading.Executors;

public class DCABuyExecutor : IExecutor
{
    public Task Execute(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
