using MediatR;

namespace Trading.API.Application.Commands;

public class CreateCredentialCommand : IRequest<string>
{
    public required string ApiKey { get; set; }
    public required string ApiSecret { get; set; }
}