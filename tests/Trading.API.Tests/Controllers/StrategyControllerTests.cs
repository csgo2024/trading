using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Trading.API.Controllers;
using Trading.Application.Commands;
using Trading.Application.Queries;
using Trading.Common.Models;
using Trading.Domain.Entities;

namespace Trading.API.Tests.Controllers;

public class StrategyControllerTests
{
    private readonly Mock<IStrategyQuery> _mockStrategyQuery;
    private readonly Mock<IMediator> _mockMediator;
    private readonly StrategyController _controller;

    public StrategyControllerTests()
    {
        _mockStrategyQuery = new Mock<IStrategyQuery>();
        _mockMediator = new Mock<IMediator>();
        _controller = new StrategyController(_mockStrategyQuery.Object, _mockMediator.Object);
    }

    [Fact]
    public async Task GetStrategyList_WithValidRequest_ShouldReturnPagedResult()
    {
        // Arrange
        var request = new PagedRequest { PageIndex = 1, PageSize = 10 };
        var expectedResult = new PagedResult<Strategy>(new List<Strategy> { new() { Id = "1", Symbol = "BTCUSDT" } }, 1, 1, 10);

        _mockStrategyQuery
            .Setup(x => x.GetStrategyListAsync(request, CancellationToken.None))
            .ReturnsAsync(expectedResult);

        // Act
        var actionResult = await _controller.GetStrategyList(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var apiResponse = Assert.IsType<ApiResponse<PagedResult<Strategy>>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(expectedResult.TotalCount, apiResponse.Data.TotalCount);
        Assert.Equal(expectedResult.Items.First().Symbol, apiResponse.Data.Items.First().Symbol);
    }

    [Fact]
    public async Task AddStrategy_WithValidCommand_ShouldReturnSuccess()
    {
        // Arrange
        var command = new CreateStrategyCommand
        {
            Symbol = "BTCUSDT",
            Amount = 100,
            PriceDropPercentage = 0.1m,
            AccountType = AccountType.Spot
        };

        _mockMediator
            .Setup(x => x.Send(It.IsAny<CreateStrategyCommand>(), default))
            .ReturnsAsync(new Strategy());

        // Act
        var actionResult = await _controller.AddStrategy(command);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var apiResponse = Assert.IsType<ApiResponse<Strategy>>(okResult.Value);
        Assert.True(apiResponse.Success);
        _mockMediator.Verify(x => x.Send(command, default), Times.Once);
    }

    [Fact]
    public async Task DeleteStrategy_WithValidId_ShouldReturnSuccess()
    {
        // Arrange
        var id = "test-id";
        _mockMediator
            .Setup(x => x.Send(It.Is<DeleteStrategyCommand>(c => c.Id == id), default))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _controller.DeleteStrategy(id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var apiResponse = Assert.IsType<ApiResponse<bool>>(okResult.Value);
        Assert.True(apiResponse.Success);
        _mockMediator.Verify(
            x => x.Send(It.Is<DeleteStrategyCommand>(c => c.Id == id), default),
            Times.Once);
    }

    [Fact]
    public async Task GetStrategyById_WithExistingId_ShouldReturnStrategy()
    {
        // Arrange
        var id = "test-id";
        var expectedStrategy = new Strategy
        {
            Id = id,
            Symbol = "BTCUSDT",
            AccountType = AccountType.Spot
        };

        _mockStrategyQuery
            .Setup(x => x.GetStrategyByIdAsync(id, CancellationToken.None))
            .ReturnsAsync(expectedStrategy);

        // Act
        var actionResult = await _controller.GetStrategyById(id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var apiResponse = Assert.IsType<ApiResponse<Strategy>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(expectedStrategy.Id, apiResponse.Data.Id);
        Assert.Equal(expectedStrategy.Symbol, apiResponse.Data.Symbol);
    }

    [Fact]
    public async Task GetStrategyById_WithNonExistingId_ShouldReturnNotFound()
    {
        // Arrange
        var id = "non-existing-id";
        _mockStrategyQuery
            .Setup(x => x.GetStrategyByIdAsync(id, CancellationToken.None))
            .ReturnsAsync(value: null);

        // Act
        var actionResult = await _controller.GetStrategyById(id);

        // Assert
        Assert.IsType<NotFoundResult>(actionResult);
    }
}
