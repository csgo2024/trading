using MediatR;

namespace Trading.API.Application.Commands;

public class DeleteStrategyCommand : IRequest<bool>
{
    public required string Id { get; set; }

}
