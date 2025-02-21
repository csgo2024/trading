using Trading.Domain.Entities;

namespace Trading.API.Application.Queries
{
    public interface ICredentialQuery
    {
        CredentialSettings? GetCredential();

    }
}
