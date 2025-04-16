using Trading.Application.Services.Trading.Account;
using Trading.Domain.Entities;

namespace Trading.Application.Services.Trading.Executors;

public interface IExecutor
{
    Task Execute(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct);

}
