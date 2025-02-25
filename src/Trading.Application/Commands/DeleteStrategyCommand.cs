using MediatR;

namespace Trading.Application.Commands;

public class DeleteStrategyCommand : IRequest<bool>
{
    public required string Id { get; set; }

}
