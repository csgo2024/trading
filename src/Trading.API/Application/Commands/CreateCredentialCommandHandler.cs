using System.Security.Cryptography;
using System.Text;
using MediatR;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.API.Application.Commands
{
    public class CreateCredentialCommandHandler : IRequestHandler<CreateCredentialCommand, string>
    {
        private readonly ICredentialSettingRepository _credentialSettingRepository;
        public CreateCredentialCommandHandler(ICredentialSettingRepository credentialSettingRepository)
        {
            _credentialSettingRepository = credentialSettingRepository;
        }

        public static byte[] EncryptData(string data, string publicKey)
        {
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(publicKey);

                byte[] dataBytes = Encoding.UTF8.GetBytes(data);
                byte[] encryptedData = rsa.Encrypt(dataBytes, false); // false 表示不使用 OAEP
                return encryptedData;
            }
        }

        // 使用私钥解密数据
        public static byte[] DecryptData(byte[] encryptedBytes, string privateKey)
        {
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(privateKey);

                byte[] decryptedData = rsa.Decrypt(encryptedBytes, false); // false 表示不使用 OAEP
                return decryptedData;
            }
        }

        public async Task<string> Handle(CreateCredentialCommand request, CancellationToken cancellationToken)
        {
            var entity = new CredentialSettings();
            entity.CreatedAt = DateTime.Now;
            string privateKey = "";
            // 生成密钥对
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                string publicKey = rsa.ToXmlString(false); // 公钥
                privateKey = rsa.ToXmlString(true); // 私钥

                entity.ApiKey = EncryptData(request.ApiKey, publicKey);
                entity.ApiSecret = EncryptData(request.ApiSecret, publicKey);
            }
            entity.Status = 1;
            await _credentialSettingRepository.EmptyAsync(cancellationToken);
            await _credentialSettingRepository.AddAsync(entity, cancellationToken);
            return privateKey;
        }
    }
}
