using Microsoft.Extensions.Logging;
using Trading.Application.Services.Trading.Account;
using Trading.Domain.Entities;

namespace Trading.Application.Services.Trading.Executors;

public class DCABuyExecutor : BaseExecutor
{
    public DCABuyExecutor(ILogger<BaseExecutor> logger) : base(logger)
    {
    }

    public override Task Execute(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
