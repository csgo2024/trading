using System.Security.Cryptography;
using System.Text;

namespace Trading.Common.Helpers;

/// <summary>
/// Provides RSA encryption and decryption functionality
/// </summary>
public static class RsaEncryptionHelper
{
    private const int MinimumKeySize = 2048;

    /// <summary>
    /// Encrypts data using RSA public key
    /// </summary>
    /// <param name="data">Data to encrypt</param>
    /// <param name="publicKey">RSA public key in XML format</param>
    /// <returns>Encrypted bytes</returns>
    public static byte[] EncryptToBytes(string data, string publicKey)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(publicKey);

        using var rsa = new RSACryptoServiceProvider(MinimumKeySize);
        rsa.FromXmlString(publicKey);

        var dataBytes = Encoding.UTF8.GetBytes(data);
        return rsa.Encrypt(dataBytes, false);
    }

    /// <summary>
    /// Decrypts data using RSA private key
    /// </summary>
    /// <param name="encryptedBytes">Encrypted data</param>
    /// <param name="privateKey">RSA private key in XML format</param>
    /// <returns>Decrypted string</returns>
    public static string DecryptFromBytes(byte[] encryptedBytes, string privateKey)
    {
        ArgumentNullException.ThrowIfNull(encryptedBytes);
        ArgumentNullException.ThrowIfNull(privateKey);

        using var rsa = new RSACryptoServiceProvider(MinimumKeySize);
        rsa.FromXmlString(privateKey);

        var bytes = rsa.Decrypt(encryptedBytes, false);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Encrypts a string to Base64 encoded string using RSA public key
    /// </summary>
    public static string EncryptToBase64(string data, string publicKey)
    {
        var encryptedBytes = EncryptToBytes(data, publicKey);
        return Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// Decrypts a Base64 encoded string using RSA private key
    /// </summary>
    public static string DecryptFromBase64(string encryptedBase64String, string privateKey)
    {
        ArgumentNullException.ThrowIfNull(encryptedBase64String);

        var encryptedBytes = Convert.FromBase64String(encryptedBase64String);
        var result = DecryptFromBytes(encryptedBytes, privateKey);
        return result;
    }

    /// <summary>
    /// Generates RSA key pair and encrypts API credentials
    /// </summary>
    public static (string EncryptedKey, string EncryptedSecret, string PrivateKey) EncryptApiCredentialToBase64(string apiKey,
                                                                                                                string apiSecret)
    {
        var (key, secret, privateKey) = EncryptApiCredentialToBytes(apiKey, apiSecret);
        var encryptedKey = Convert.ToBase64String(key);
        var encryptedSecret = Convert.ToBase64String(secret);
        return (encryptedKey, encryptedSecret, privateKey);
    }

    /// <summary>
    /// Decrypts API credentials using RSA private key
    /// </summary>
    public static (string ApiKey, string ApiSecret) DecryptApiCredentialFromBase64(string encryptedKey,
                                                                                   string encryptedSecret,
                                                                                   string privateKey)
    {
        var apiKey = DecryptFromBase64(encryptedKey, privateKey);
        var apiSecret = DecryptFromBase64(encryptedSecret, privateKey);
        return (apiKey, apiSecret);
    }
    /// <summary>
    /// Generates RSA key pair and encrypts API credentials
    /// </summary>
    public static (byte[] EncryptedKey, byte[] EncryptedSecret, string PrivateKey) EncryptApiCredentialToBytes(string apiKey,
                                                                                                               string apiSecret)
    {
        ArgumentNullException.ThrowIfNull(apiKey);
        ArgumentNullException.ThrowIfNull(apiSecret);

        using var rsa = new RSACryptoServiceProvider(MinimumKeySize);
        var publicKey = rsa.ToXmlString(false);
        var privateKey = rsa.ToXmlString(true);

        var encryptedKey = EncryptToBytes(apiKey, publicKey);
        var encryptedSecret = EncryptToBytes(apiSecret, publicKey);

        return (encryptedKey, encryptedSecret, privateKey);
    }

    /// <summary>
    /// Decrypts API credentials using RSA private key
    /// </summary>
    public static (string ApiKey, string ApiSecret) DecryptApiCredentialFromBytes(byte[] encryptedKey,
                                                                                  byte[] encryptedSecret,
                                                                                  string privateKey)
    {
        var apiKey = DecryptFromBytes(encryptedKey, privateKey);
        var apiSecret = DecryptFromBytes(encryptedSecret, privateKey);
        return (apiKey, apiSecret);
    }
}
