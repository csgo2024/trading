using Moq;
using Trading.Application.Commands;
using Trading.Common.Helpers;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Commands;

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

        _mockRepository.Setup(x => x.AddAsync(It.IsAny<CredentialSetting>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new CredentialSetting()));  // Changed to return Task<CredentialSetting>

        // Act
        var privateKey = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEmpty(privateKey);
        _mockRepository.Verify(x => x.EmptyAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(x => x.AddAsync(It.IsAny<CredentialSetting>(), It.IsAny<CancellationToken>()), Times.Once);
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

        CredentialSetting capturedEntity = new CredentialSetting();
        _mockRepository.Setup(x => x.AddAsync(It.IsAny<CredentialSetting>(), It.IsAny<CancellationToken>()))
            .Callback<CredentialSetting, CancellationToken>((entity, _) => capturedEntity = entity)
            .Returns(Task.FromResult(capturedEntity));  // Changed to return Task<CredentialSetting>

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
        Assert.Equal(command.ApiKey, RsaEncryptionHelper.DecryptFromBytes(capturedEntity.ApiKey, privateKey));
        Assert.Equal(command.ApiSecret, RsaEncryptionHelper.DecryptFromBytes(capturedEntity.ApiSecret, privateKey));
    }
}
