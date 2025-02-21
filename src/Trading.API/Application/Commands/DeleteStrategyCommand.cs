using MediatR;

namespace Trading.API.Application.Commands;

public class DeleteStrategyCommand: IRequest<bool>
{
    public string Id { get; set; }
    
}