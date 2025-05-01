using System.Security.Cryptography;
using System.Text;

namespace Trading.Common.Helpers;

public static class RsaEncryptionHelper
{

    #region V1
    public static byte[] EncryptDataV1(string data, string publicKey)
    {
        using (var rsa = new RSACryptoServiceProvider())
        {
            rsa.FromXmlString(publicKey);

            var dataBytes = Encoding.UTF8.GetBytes(data);
            var encryptedData = rsa.Encrypt(dataBytes, false); // false 表示不使用 OAEP
            return encryptedData;
        }
    }

    public static byte[] DecryptDataV1(byte[] encryptedBytes, string privateKey)
    {
        using (var rsa = new RSACryptoServiceProvider())
        {
            rsa.FromXmlString(privateKey);

            var decryptedData = rsa.Decrypt(encryptedBytes, false); // false 表示不使用 OAEP
            return decryptedData;
        }
    }
    #endregion

    private static string EncryptData(string data, string publicKey)
    {
        using (var rsa = new RSACryptoServiceProvider())
        {
            rsa.FromXmlString(publicKey);

            var dataBytes = Encoding.UTF8.GetBytes(data);
            var encryptedBytes = rsa.Encrypt(dataBytes, false); // false 表示不使用 OAEP
            var encrypted64Str = Convert.ToBase64String(encryptedBytes);
            return encrypted64Str;
        }
    }

    private static string DecryptData(string encrypted64Str, string privateKey)
    {
        using (var rsa = new RSACryptoServiceProvider())
        {
            rsa.FromXmlString(privateKey);
            var encrypted64Bytes = Convert.FromBase64String(encrypted64Str);
            var decryptedBytes = rsa.Decrypt(encrypted64Bytes, false); // false 表示不使用 OAEP
            var decryptedStr = Encoding.UTF8.GetString(decryptedBytes);

            return decryptedStr;
        }
    }

    public static (string EncryptedKey, string EncryptedSecret, string privateKey) EncryptApiCredential(string apiKey, string apiSecret)
    {
        var encryptedKey = "";
        var encryptedSecret = "";
        var privateKey = "";

        using (var rsa = new RSACryptoServiceProvider(2048))
        {
            var publicKey = rsa.ToXmlString(false);
            privateKey = rsa.ToXmlString(true);

            encryptedKey = EncryptData(apiKey, publicKey);
            encryptedSecret = EncryptData(apiSecret, publicKey);
        }
        return (encryptedKey, encryptedSecret, privateKey);
    }

    public static (string ApiKey, string ApiSecret) DecryptApiCredential(string encryptedKey, string encryptedSecret, string privateKey)
    {
        var ApiKey = DecryptData(encryptedKey, privateKey);
        var ApiSecret = DecryptData(encryptedSecret, privateKey);
        return (ApiKey, ApiSecret);
    }
}
