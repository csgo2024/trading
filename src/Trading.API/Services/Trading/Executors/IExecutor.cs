using Trading.API.Services.Trading.Account;
using Trading.Domain.Entities;

namespace Trading.API.Services.Trading.Executors;

public interface IExecutor
{
    Task Execute(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct);

}
