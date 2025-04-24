using System.Text;

namespace Trading.Common.Tests.Helpers;

public class RsaEncryptionHelperTests
{

    [Fact]
    public void EncryptDecryptV1_WithSimpleString_ShouldReturnOriginalValue()
    {
        // Arrange
        string originalText = "Hello World";
        using var rsa = new System.Security.Cryptography.RSACryptoServiceProvider(2048);
        string publicKey = rsa.ToXmlString(false);
        string privateKey = rsa.ToXmlString(true);

        // Act
        byte[] encryptedData = RsaEncryptionHelper.EncryptDataV1(originalText, publicKey);
        byte[] decryptedData = RsaEncryptionHelper.DecryptDataV1(encryptedData, privateKey);
        string decryptedText = Encoding.UTF8.GetString(decryptedData);

        // Assert
        Assert.Equal(originalText, decryptedText);
    }

    [Fact]
    public void EncryptDecryptV1_WithSpecialCharacters_ShouldReturnOriginalValue()
    {
        // Arrange
        string originalText = "!@#$%^&*()_+-=[]{}|;:,.<>?";
        using var rsa = new System.Security.Cryptography.RSACryptoServiceProvider(2048);
        string publicKey = rsa.ToXmlString(false);
        string privateKey = rsa.ToXmlString(true);

        // Act
        byte[] encryptedData = RsaEncryptionHelper.EncryptDataV1(originalText, publicKey);
        byte[] decryptedData = RsaEncryptionHelper.DecryptDataV1(encryptedData, privateKey);
        string decryptedText = Encoding.UTF8.GetString(decryptedData);

        // Assert
        Assert.Equal(originalText, decryptedText);
    }

    [Fact]
    public void EncryptDecryptV1_WithChineseCharacters_ShouldReturnOriginalValue()
    {
        // Arrange
        string originalText = "你好世界";
        using var rsa = new System.Security.Cryptography.RSACryptoServiceProvider(2048);
        string publicKey = rsa.ToXmlString(false);
        string privateKey = rsa.ToXmlString(true);

        // Act
        byte[] encryptedData = RsaEncryptionHelper.EncryptDataV1(originalText, publicKey);
        byte[] decryptedData = RsaEncryptionHelper.DecryptDataV1(encryptedData, privateKey);
        string decryptedText = Encoding.UTF8.GetString(decryptedData);

        // Assert
        Assert.Equal(originalText, decryptedText);
    }

    [Fact]
    public void EncryptDecryptV1_WithEmptyString_ShouldReturnEmptyString()
    {
        // Arrange
        string originalText = "";
        using var rsa = new System.Security.Cryptography.RSACryptoServiceProvider(2048);
        string publicKey = rsa.ToXmlString(false);
        string privateKey = rsa.ToXmlString(true);

        // Act
        byte[] encryptedData = RsaEncryptionHelper.EncryptDataV1(originalText, publicKey);
        byte[] decryptedData = RsaEncryptionHelper.DecryptDataV1(encryptedData, privateKey);
        string decryptedText = Encoding.UTF8.GetString(decryptedData);

        // Assert
        Assert.Equal(originalText, decryptedText);
    }

    [Fact]
    public void EncryptV1_ShouldProduceDifferentOutput_ThanOriginalData()
    {
        // Arrange
        string originalText = "test_data";
        using var rsa = new System.Security.Cryptography.RSACryptoServiceProvider(2048);
        string publicKey = rsa.ToXmlString(false);

        // Act
        byte[] encryptedData = RsaEncryptionHelper.EncryptDataV1(originalText, publicKey);

        // Assert
        Assert.NotEqual(originalText, Encoding.UTF8.GetString(encryptedData));
        Assert.True(encryptedData.Length > 0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid_key")]
    public void EncryptV1_WithInvalidPublicKey_ShouldThrowCryptographicException(string invalidKey)
    {
        // Arrange
        string originalText = "test_data";

        // Act & Assert
        Assert.Throws<System.Security.Cryptography.CryptographicException>(
            () => RsaEncryptionHelper.EncryptDataV1(originalText, invalidKey)
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid_key")]
    public void DecryptV1_WithInvalidPrivateKey_ShouldThrowCryptographicException(string invalidKey)
    {
        // Arrange
        byte[] someData = new byte[] { 1, 2, 3, 4, 5 };

        // Act & Assert
        Assert.Throws<System.Security.Cryptography.CryptographicException>(
            () => RsaEncryptionHelper.DecryptDataV1(someData, invalidKey)
        );
    }

    [Fact]
    public void EncryptDecrypt_WithSimpleString_ShouldReturnOriginalValue()
    {
        // Arrange
        string originalKey = "Hello World";
        string originalSecret = "dummy";

        // Act
        var (encryptedKey, encryptedSecret, privateKey) = RsaEncryptionHelper.EncryptApiCredential(originalKey, originalSecret);
        var (decryptedKey, _) = RsaEncryptionHelper.DecryptApiCredential(encryptedKey, encryptedSecret, privateKey);

        // Assert
        Assert.Equal(originalKey, decryptedKey);
    }

    [Fact]
    public void EncryptDecrypt_WithSpecialCharacters_ShouldReturnOriginalValue()
    {
        string originalKey = "!@#$%^&*()_+-=[]{}|;:,.<>?";
        string originalSecret = "api_secret_456";

        // Act
        var (encryptedKey, encryptedSecret, privateKey) = RsaEncryptionHelper.EncryptApiCredential(originalKey, originalSecret);
        var (decryptedKey, decryptedSecret) = RsaEncryptionHelper.DecryptApiCredential(encryptedKey, encryptedSecret, privateKey);

        // Assert
        Assert.Equal(originalKey, decryptedKey);
        Assert.Equal(originalSecret, decryptedSecret);
    }

    [Fact]
    public void EncryptDecrypt_WithChineseCharacters_ShouldReturnOriginalValue()
    {
        string originalKey = "你好世界";
        string originalSecret = "api_secret_456";

        // Act
        var (encryptedKey, encryptedSecret, privateKey) = RsaEncryptionHelper.EncryptApiCredential(originalKey, originalSecret);
        var (decryptedKey, decryptedSecret) = RsaEncryptionHelper.DecryptApiCredential(encryptedKey, encryptedSecret, privateKey);

        // Assert
        Assert.Equal(originalKey, decryptedKey);
        Assert.Equal(originalSecret, decryptedSecret);
    }

    [Fact]
    public void EncryptDecrypt_WithApiCredentials_ShouldReturnOriginalValues()
    {
        // Arrange
        string originalKey = "api_key_123";
        string originalSecret = "api_secret_456";

        // Act
        var (encryptedKey, encryptedSecret, privateKey) = RsaEncryptionHelper.EncryptApiCredential(originalKey, originalSecret);
        var (decryptedKey, decryptedSecret) = RsaEncryptionHelper.DecryptApiCredential(encryptedKey, encryptedSecret, privateKey);

        // Assert
        Assert.Equal(originalKey, decryptedKey);
        Assert.Equal(originalSecret, decryptedSecret);
    }

    [Fact]
    public void EncryptedValue_ShouldBeDifferentFromOriginal()
    {
        // Arrange
        string originalText = "test_value";

        // Act
        var (encryptedText, _, _) = RsaEncryptionHelper.EncryptApiCredential(originalText, "dummy");

        // Assert
        Assert.NotEqual(originalText, encryptedText);
        Assert.True(Convert.FromBase64String(encryptedText).Length > 0);
    }
}
