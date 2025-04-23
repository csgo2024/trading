namespace Trading.Common.Tests.Helpers;

public class RsaEncryptionHelperTests
{
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
