using System.ComponentModel.DataAnnotations;
using MediatR;
using Trading.Domain.Entities;

namespace Trading.Application.Commands;

public class CreateStrategyCommand : IRequest<Strategy>
{
    [Required(ErrorMessage = "Symbol cannot be empty")]
    public string Symbol { get; set; } = "";

    [Range(10, int.MaxValue, ErrorMessage = "Amount must be greater than 10")]
    public int Amount { get; set; }

    [Range(0.01, 0.9, ErrorMessage = "Volatility must be between 0.01 and 0.9")]
    public decimal Volatility { get; set; }

    [Range(1, 20, ErrorMessage = "Leverage must be between 1 and 20")]
    public int? Leverage { get; set; }

    public AccountType AccountType { get; set; } = AccountType.Spot;

    public StrategyType StrategyType { get; set; } = StrategyType.BottomBuy;
}
