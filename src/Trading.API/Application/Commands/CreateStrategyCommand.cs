using MediatR;
using Trading.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace Trading.API.Application.Commands;

public class CreateStrategyCommand : IRequest<Strategy>
{
    public string Symbol { get; set; } = string.Empty;
    
    [Range(10, int.MaxValue, ErrorMessage = "Amount must be greater than 10")]
    public int Amount { get; set; }
    
    [Range(0.01, 0.9, ErrorMessage = "PriceDropPercentage must be between 0.1 and 0.9")]
    public decimal PriceDropPercentage { get; set; }
    
    [Range(1, 20, ErrorMessage = "Leverage must be between 1 and 20")]
    public int Leverage { get; set; }

    public AccountType AccountType { get; set; } = AccountType.Spot;

    public StrategyType StrategyType { get; set; } = StrategyType.BuyBottom;
}