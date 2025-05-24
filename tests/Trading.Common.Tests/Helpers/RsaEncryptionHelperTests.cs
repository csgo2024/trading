using System.Security.Cryptography;
using Trading.Common.Helpers;

namespace Trading.Common.Tests.Helpers;

public class RsaEncryptionHelperTests
{
    private const string TestData = "Hello World!";
    private const string TestApiKey = "test-api-key";
    private const string TestApiSecret = "test-api-secret";

    [Fact]
    public void EncryptToBytes_ShouldEncryptAndDecrypt()
    {
        // Arrange
        using var rsa = new RSACryptoServiceProvider(2048);
        var publicKey = rsa.ToXmlString(false);
        var privateKey = rsa.ToXmlString(true);

        // Act
        var encrypted = RsaEncryptionHelper.EncryptToBytes(TestData, publicKey);
        var decrypted = RsaEncryptionHelper.DecryptFromBytes(encrypted, privateKey);

        // Assert
        Assert.Equal(TestData, decrypted);
    }

    [Fact]
    public void EncryptToBase64_ShouldEncryptAndDecrypt()
    {
        // Arrange
        using var rsa = new RSACryptoServiceProvider(2048);
        var publicKey = rsa.ToXmlString(false);
        var privateKey = rsa.ToXmlString(true);

        // Act
        var encrypted = RsaEncryptionHelper.EncryptToBase64(TestData, publicKey);
        var decrypted = RsaEncryptionHelper.DecryptFromBase64(encrypted, privateKey);

        // Assert
        Assert.Equal(TestData, decrypted);
    }

    [Fact]
    public void EncryptApiCredentialToBase64_ShouldEncryptAndDecrypt()
    {
        // Act
        var (encryptedKey, encryptedSecret, privateKey) =
            RsaEncryptionHelper.EncryptApiCredentialToBase64(TestApiKey, TestApiSecret);
        var (decryptedKey, decryptedSecret) =
            RsaEncryptionHelper.DecryptApiCredentialFromBase64(encryptedKey, encryptedSecret, privateKey);

        // Assert
        Assert.Equal(TestApiKey, decryptedKey);
        Assert.Equal(TestApiSecret, decryptedSecret);
    }

    [Fact]
    public void EncryptApiCredentialToBytes_ShouldEncryptAndDecrypt()
    {
        // Act
        var (encryptedKey, encryptedSecret, privateKey) =
            RsaEncryptionHelper.EncryptApiCredentialToBytes(TestApiKey, TestApiSecret);
        var (decryptedKey, decryptedSecret) =
            RsaEncryptionHelper.DecryptApiCredentialFromBytes(encryptedKey, encryptedSecret, privateKey);

        // Assert
        Assert.Equal(TestApiKey, decryptedKey);
        Assert.Equal(TestApiSecret, decryptedSecret);
    }

    [Fact]
    public void EncryptionMethods_ShouldGenerateDifferentCiphertext()
    {
        // Arrange
        using var rsa = new RSACryptoServiceProvider(2048);
        var publicKey = rsa.ToXmlString(false);

        // Act
        var encrypted1 = RsaEncryptionHelper.EncryptToBase64(TestData, publicKey);
        var encrypted2 = RsaEncryptionHelper.EncryptToBase64(TestData, publicKey);

        // Assert
        Assert.NotEqual(encrypted1, encrypted2);
    }

    [Fact]
    public void KeyGeneration_ShouldGenerateUniqueKeys()
    {
        // Act
        var (_, _, privateKey1) = RsaEncryptionHelper.EncryptApiCredentialToBytes(TestApiKey, TestApiSecret);
        var (_, _, privateKey2) = RsaEncryptionHelper.EncryptApiCredentialToBytes(TestApiKey, TestApiSecret);

        // Assert
        Assert.NotEqual(privateKey1, privateKey2);
    }
}
