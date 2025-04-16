using Trading.Application.Services.Trading.Account;
using Trading.Domain.Entities;

namespace Trading.Application.Services.Trading.Executors;

public class DCABuyExecutor : IExecutor
{
    public Task Execute(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
