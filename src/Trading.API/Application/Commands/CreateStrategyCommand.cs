using MediatR;
using Trading.Domain.Entities;

namespace Trading.API.Application.Commands;

public class CreateStrategyCommand : IRequest<bool>
{
    public string Symbol { get; set; } = string.Empty;
    public int Amount { get; set; }
    public decimal PriceDropPercentage { get; set; }
    public int Leverage { get; set; }
    public StrategyType StrategyType { get; set; }
}