using Binance.Net.Interfaces.Clients.SpotApi;
using Binance.Net.Interfaces.Clients.UsdFuturesApi;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Trading.Application.Services.Trading.Account;
using Trading.Common.Enums;
using Trading.Exchange.Binance.Wrappers.Clients;

namespace Trading.Application.Tests.Services.Trading.Account;

public class AccountProcessorFactoryTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AccountProcessorFactory _factory;

    public AccountProcessorFactoryTests()
    {
        var services = new ServiceCollection();

        // Setup mock dependencies for SpotProcessor and FutureProcessor
        var usdFutureRestClientMock = new Mock<BinanceRestClientUsdFuturesApiWrapper>(
            Mock.Of<IBinanceRestClientUsdFuturesApiAccount>(),
            Mock.Of<IBinanceRestClientUsdFuturesApiExchangeData>(),
            Mock.Of<IBinanceRestClientUsdFuturesApiTrading>());
        var spotRestClientMock = new Mock<BinanceRestClientSpotApiWrapper>(
            Mock.Of<IBinanceRestClientSpotApiAccount>(),
            Mock.Of<IBinanceRestClientSpotApiExchangeData>(),
            Mock.Of<IBinanceRestClientSpotApiTrading>());

        // Register processors and their dependencies
        services.AddSingleton(usdFutureRestClientMock.Object);
        services.AddSingleton(spotRestClientMock.Object);
        services.AddScoped<SpotProcessor>();
        services.AddScoped<FutureProcessor>();

        _serviceProvider = services.BuildServiceProvider();
        _factory = new AccountProcessorFactory(_serviceProvider);
    }

    [Theory]
    [InlineData(AccountType.Spot, typeof(SpotProcessor))]
    [InlineData(AccountType.Future, typeof(FutureProcessor))]
    public void GetAccountProcessor_WithValidType_ShouldReturnCorrectProcessor(AccountType type, Type expectedType)
    {
        // Act
        var processor = _factory.GetAccountProcessor(type);

        // Assert
        Assert.NotNull(processor);
        Assert.IsType(expectedType, processor);
    }

    [Fact]
    public void GetAccountProcessor_WithInvalidType_ShouldReturnNull()
    {
        // Arrange
        var invalidType = (AccountType)999;

        // Act
        var processor = _factory.GetAccountProcessor(invalidType);

        // Assert
        Assert.Null(processor);
    }

    [Fact]
    public void Constructor_ShouldInitializeAllAccountTypes()
    {
        // Arrange
        var expectedTypes = new[] { AccountType.Spot, AccountType.Future };

        // Act
        var results = expectedTypes.Select(_factory.GetAccountProcessor);

        // Assert
        Assert.All(results, Assert.NotNull);
        Assert.Equal(expectedTypes.Length, results.Count());
    }

    [Fact]
    public void GetAccountProcessor_WithAllTypes_ShouldReturnCorrectImplementations()
    {
        // Arrange
        var accountTypes = Enum.GetValues(typeof(AccountType)).Cast<AccountType>();

        // Act & Assert
        foreach (var accountType in accountTypes)
        {
            var processor = _factory.GetAccountProcessor(accountType);
            Assert.NotNull(processor);

            switch (accountType)
            {
                case AccountType.Spot:
                    Assert.IsType<SpotProcessor>(processor);
                    break;
                case AccountType.Future:
                    Assert.IsType<FutureProcessor>(processor);
                    break;
                default:
                    Assert.Fail($"Unexpected account type: {accountType}");
                    break;
            }
        }
    }
}
