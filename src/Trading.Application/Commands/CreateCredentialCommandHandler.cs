using MediatR;
using Trading.Common.Helpers;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Commands;

public class CreateCredentialCommandHandler : IRequestHandler<CreateCredentialCommand, string>
{
    private readonly ICredentialSettingRepository _credentialSettingRepository;
    public CreateCredentialCommandHandler(ICredentialSettingRepository credentialSettingRepository)
    {
        _credentialSettingRepository = credentialSettingRepository;
    }

    public async Task<string> Handle(CreateCredentialCommand request, CancellationToken cancellationToken)
    {
        var (key, secret, privateKey) = RsaEncryptionHelper.EncryptApiCredentialToBytes(request.ApiKey, request.ApiSecret);
        var entity = new CredentialSetting
        {
            CreatedAt = DateTime.Now,
            ApiKey = key,
            ApiSecret = secret,
            Status = 1
        };
        await _credentialSettingRepository.EmptyAsync(cancellationToken);
        await _credentialSettingRepository.AddAsync(entity, cancellationToken);
        return privateKey;
    }
}
