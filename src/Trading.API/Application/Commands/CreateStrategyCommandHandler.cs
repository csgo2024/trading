using MediatR;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.API.Application.Commands
{
    public class CreateStrategyCommandHandler : IRequestHandler<CreateStrategyCommand, bool>
    {
        private readonly IStrategyRepository _strategyRepository;

        public CreateStrategyCommandHandler(IStrategyRepository strategyRepository)
        {
            _strategyRepository = strategyRepository;
        }
        public async Task<bool> Handle(CreateStrategyCommand request, CancellationToken cancellationToken)
        {
            var entity = new Strategy();
            entity.CreatedAt = DateTime.Now;
            entity.PriceDropPercentage = request.PriceDropPercentage;
            entity.StrategyType = request.StrategyType;
            entity.Symbol = request.Symbol;
            entity.Amount = request.Amount;
            entity.Leverage = request.Leverage;
            entity.Status = StrateStatus.Running;
            await _strategyRepository.Add(entity);
            return true;
        }
    }
}
