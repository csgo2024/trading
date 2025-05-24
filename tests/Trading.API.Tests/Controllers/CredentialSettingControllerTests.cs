using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Trading.API.Controllers;
using Trading.Application.Commands;
using Trading.Common.Models;

namespace Trading.API.Tests.Controllers;

public class CredentialSettingControllerTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly CredentialSettingController _controller;

    public CredentialSettingControllerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _controller = new CredentialSettingController(_mockMediator.Object);
    }

    [Fact]
    public async Task Add_WithValidCommand_ShouldReturnSuccessResponse()
    {
        // Arrange
        var command = new CreateCredentialCommand
        {
            ApiKey = "testApiKey",
            ApiSecret = "testSecretKey"
        };
        var expectedId = "test-credential-id";

        _mockMediator
            .Setup(x => x.Send(command, CancellationToken.None))
            .ReturnsAsync(expectedId);

        // Act
        var actionResult = await _controller.Add(command);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var apiResponse = Assert.IsType<ApiResponse<string>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.Equal(expectedId, apiResponse.Data);
        _mockMediator.Verify(x => x.Send(command, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Add_WhenMediatorThrows_ShouldReturnErrorResponse()
    {
        // Arrange
        var command = new CreateCredentialCommand
        {
            ApiKey = "testApiKey",
            ApiSecret = "testSecretKey"
        };

        _mockMediator
            .Setup(x => x.Send(command, CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("Invalid credentials"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _controller.Add(command));
    }
}
