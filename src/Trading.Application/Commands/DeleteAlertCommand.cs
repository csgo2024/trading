using MediatR;

namespace Trading.Application.Commands;

public class DeleteAlertCommand : IRequest<bool>
{
    public required string Id { get; set; }

}
