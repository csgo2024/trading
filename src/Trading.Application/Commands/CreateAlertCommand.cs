using System.ComponentModel.DataAnnotations;
using MediatR;
using Trading.Common.Attributes;
using Trading.Domain.Entities;
using Trading.Exchange.Binance.Attributes;

namespace Trading.Application.Commands;

public class CreateAlertCommand : IRequest<Alert>
{
    [Required(ErrorMessage = "Symbol cannot be empty")]
    public string Symbol { get; set; } = "";

    [Interval(Required = true)]
    public string Interval { get; set; } = "4h";

    [JavaScript(Required = true)]
    public string Expression { get; set; } = "";
}
