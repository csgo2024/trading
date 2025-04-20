using MediatR;

namespace Trading.Application.Commands;

public class DeleteAlarmCommand : IRequest<bool>
{
    public required string Id { get; set; }

}
