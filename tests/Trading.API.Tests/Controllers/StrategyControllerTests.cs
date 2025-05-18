using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Trading.Application.Commands;
using Trading.Common.Enums;
using Trading.Common.Models;
using Trading.Domain.Entities;

namespace Trading.API.Tests.Controllers;

public class StrategyControllerTests : IClassFixture<TradingApiFixture>
{
    private readonly HttpClient Client;
    protected readonly TradingApiFixture Fixture;

    public StrategyControllerTests(TradingApiFixture fixture)
    {
        Client = fixture.CreateClient();
        Fixture = fixture;
    }

    [Fact]
    public async Task GetStrategyList_ReturnsSuccessResponse()
    {
        // Arrange
        await Fixture.TestDataInitializer!.ResetTestData();
        var request = new PagedRequest { PageIndex = 1, PageSize = 10 };
        var queryString = $"?pageIndex={request.PageIndex}&pageSize={request.PageSize}";

        // Act
        var response = await Client.GetAsync($"/api/v1/strategy{queryString}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<Strategy>>>();

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.NotNull(result.Data.Items);
        Assert.Equal(2, result.Data.TotalCount);
        Assert.Contains(result.Data.Items, s => s.Symbol == "BTCUSDT");
        Assert.Contains(result.Data.Items, s => s.Symbol == "ETHUSDT");
    }

    [Fact]
    public async Task GetStrategyById_WithValidId_ReturnsStrategy()
    {
        // Arrange
        await Fixture.TestDataInitializer!.ResetTestData();
        var strategyId = "test-strategy-1";

        // Act
        var response = await Client.GetAsync($"/api/v1/strategy/{strategyId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<Strategy>>();

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(strategyId, result.Data.Id);
        Assert.Equal("BTCUSDT", result.Data.Symbol);
    }

    [Fact]
    public async Task AddStrategy_WithValidCommand_ReturnsCreatedStrategy()
    {
        // Arrange
        await Fixture.TestDataInitializer!.ResetTestData();
        var command = new CreateStrategyCommand
        {
            Symbol = "SOLUSDT",
            Amount = 300,
            Volatility = 0.15m,
            AccountType = AccountType.Spot,
            StrategyType = StrategyType.BottomBuy,
            StopLossExpression = "low < 200",
            Leverage = 1
        };

        var content = new StringContent(
            JsonSerializer.Serialize(command),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/strategy", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<Strategy>>();

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(command.Symbol, result.Data.Symbol);
        Assert.Equal(command.Amount, result.Data.Amount);
        Assert.Equal(command.Volatility, result.Data.Volatility);

        // Verify created strategy can be retrieved
        var getResponse = await Client.GetAsync($"/api/v1/strategy/{result.Data.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteStrategy_WithValidId_ReturnsSuccess()
    {
        // Arrange
        await Fixture.TestDataInitializer!.ResetTestData();
        var strategyId = "test-strategy-1";

        // Act
        var response = await Client.DeleteAsync($"/api/v1/strategy/{strategyId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.Data);

        // Verify strategy is actually deleted
        var getResponse = await Client.GetAsync($"/api/v1/strategy/{strategyId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }
}
