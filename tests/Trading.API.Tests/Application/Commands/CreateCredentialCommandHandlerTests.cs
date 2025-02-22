using System.Text;
using Moq;
using Trading.API.Application.Commands;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.API.Tests.Application.Commands;

public class CreateCredentialCommandHandlerTests
{
    private readonly Mock<ICredentialSettingRepository> _mockRepository;
    private readonly CreateCredentialCommandHandler _handler;

    public CreateCredentialCommandHandlerTests()
    {
        _mockRepository = new Mock<ICredentialSettingRepository>();
        _handler = new CreateCredentialCommandHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_ShouldCreateCredentialAndReturnPrivateKey()
    {
        // Arrange
        var command = new CreateCredentialCommand 
        { 
            ApiKey = "testApiKey",
            ApiSecret = "testApiSecret"
        };

        _mockRepository.Setup(x => x.EmptyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);  // Changed to return Task<bool>

        _mockRepository.Setup(x => x.AddAsync(It.IsAny<CredentialSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new CredentialSettings()));  // Changed to return Task<CredentialSettings>

        // Act
        var privateKey = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEmpty(privateKey);
        _mockRepository.Verify(x => x.EmptyAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(x => x.AddAsync(It.IsAny<CredentialSettings>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void EncryptAndDecrypt_ShouldWorkCorrectly()
    {
        // Arrange
        var testData = "TestSecretData";
        using var rsa = new System.Security.Cryptography.RSACryptoServiceProvider(2048);
        var publicKey = rsa.ToXmlString(false);
        var privateKey = rsa.ToXmlString(true);

        // Act
        var encryptedData = CreateCredentialCommandHandler.EncryptData(testData, publicKey);
        var decryptedData = CreateCredentialCommandHandler.DecryptData(encryptedData, privateKey);
        var decryptedString = Encoding.UTF8.GetString(decryptedData);

        // Assert
        Assert.Equal(testData, decryptedString);
    }

    [Fact]
    public async Task Handle_ShouldSetCorrectEntityProperties()
    {
        // Arrange
        var command = new CreateCredentialCommand 
        { 
            ApiKey = "testApiKey",
            ApiSecret = "testApiSecret"
        };

        CredentialSettings capturedEntity = new CredentialSettings() ;
        _mockRepository.Setup(x => x.AddAsync(It.IsAny<CredentialSettings>(), It.IsAny<CancellationToken>()))
            .Callback<CredentialSettings, CancellationToken>((entity, _) => capturedEntity = entity)
            .Returns(Task.FromResult(capturedEntity));  // Changed to return Task<CredentialSettings>

        _mockRepository.Setup(x => x.EmptyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);  // Added missing setup

        // Act
        var privateKey = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedEntity);
        Assert.Equal(1, capturedEntity.Status);
        Assert.NotNull(capturedEntity.ApiKey);
        Assert.NotNull(capturedEntity.ApiSecret);
        Assert.True(capturedEntity.CreatedAt <= DateTime.Now);
        Assert.True(capturedEntity.CreatedAt > DateTime.Now.AddMinutes(-1));
        Assert.Equal(command.ApiKey, Encoding.UTF8.GetString(CreateCredentialCommandHandler.DecryptData(capturedEntity.ApiKey, privateKey)));
        Assert.Equal(command.ApiSecret, Encoding.UTF8.GetString(CreateCredentialCommandHandler.DecryptData(capturedEntity.ApiSecret, privateKey)));
    }
}