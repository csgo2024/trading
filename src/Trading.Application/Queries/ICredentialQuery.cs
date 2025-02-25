using Trading.Domain.Entities;

namespace Trading.Application.Queries;

public interface ICredentialQuery
{
    CredentialSettings? GetCredential();

}
