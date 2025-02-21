using MediatR;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.API.Application.Commands
{
    public class CreateCredentialCommandHandler : IRequestHandler<CreateCredentialCommand, bool>
    {
        private readonly ICredentialSettingRepository _credentialSettingRepository;

        public CreateCredentialCommandHandler(ICredentialSettingRepository credentialSettingRepository)
        {
            _credentialSettingRepository = credentialSettingRepository;
        }
        public async Task<bool> Handle(CreateCredentialCommand request, CancellationToken cancellationToken)
        {
            var entity = new CredentialSettings();
            entity.CreatedAt = DateTime.Now;
            entity.ApiKey = request.ApiKey;
            entity.ApiSecret = request.ApiSecret;
            entity.Status = 1;
            await _credentialSettingRepository.AddAsync(entity,cancellationToken);
            return true;
        }
    }
}
