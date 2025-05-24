using System.Net;
using System.Net.Http.Json;
using Trading.Common.Models;

namespace Trading.API.Tests.Controllers;

public class StatusControllerTests : IClassFixture<TradingApiFixture>
{
    private readonly HttpClient _client;

    public StatusControllerTests(TradingApiFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task GetAll_ReturnsOkResult_WithSettings()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/status");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var settings = await response.Content.ReadFromJsonAsync<MongoDbSettings>();
        Assert.NotNull(settings);
        Assert.Contains("mongodb://", settings.ConnectionString);
        Assert.Equal("InMemoryDbForTesting", settings.DatabaseName);
    }
}
