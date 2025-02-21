using MediatR;

namespace Trading.API.Application.Commands
{
    public class CreateCredentialCommand : IRequest<bool>
    {
        public string ApiKey { get; set; }
        public string ApiSecret { get; set; }
    }
}
