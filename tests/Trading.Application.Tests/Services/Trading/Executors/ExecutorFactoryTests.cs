using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Services.Trading.Executors;
using Trading.Common.Enums;
using Trading.Common.JavaScript;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Services.Trading.Executors;

public class ExecutorFactoryTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ExecutorFactory _factory;

    public ExecutorFactoryTests()
    {
        var services = new ServiceCollection();

        // Add logger dependencies
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Add mocked repository dependencies
        var strategyRepositoryMock = new Mock<IStrategyRepository>();
        services.AddSingleton(strategyRepositoryMock.Object);

        var accountProcessorFactory = new Mock<IAccountProcessorFactory>();
        services.AddSingleton(accountProcessorFactory.Object);
        // Register executors
        services.AddScoped<BottomBuyExecutor>();
        services.AddScoped<DCABuyExecutor>();
        services.AddScoped<TopSellExecutor>();
        services.AddScoped<JavaScriptEvaluator>();
        services.AddSingleton<IStrategyStateManager, StrategyStateManager>();

        _serviceProvider = services.BuildServiceProvider();
        _factory = new ExecutorFactory(_serviceProvider);
    }

    [Theory]
    [InlineData(StrategyType.BottomBuy, typeof(BottomBuyExecutor))]
    [InlineData(StrategyType.DCA, typeof(DCABuyExecutor))]
    [InlineData(StrategyType.TopSell, typeof(TopSellExecutor))]
    public void GetExecutor_WithValidType_ShouldReturnCorrectExecutor(StrategyType type, Type expectedType)
    {
        // Act
        var executor = _factory.GetExecutor(type);

        // Assert
        Assert.NotNull(executor);
        Assert.IsType(expectedType, executor);
    }

    [Fact]
    public void GetExecutor_WithInvalidType_ShouldReturnNull()
    {
        // Arrange
        var invalidType = (StrategyType)999;

        // Act
        var executor = _factory.GetExecutor(invalidType);

        // Assert
        Assert.Null(executor);
    }

    [Fact]
    public void Constructor_ShouldInitializeAllStrategies()
    {
        // Arrange
        var expectedStrategies = new[] { StrategyType.BottomBuy, StrategyType.DCA, StrategyType.TopSell };

        // Act
        var results = expectedStrategies.Select(_factory.GetExecutor);

        // Assert
        Assert.All(results, Assert.NotNull);
        Assert.Equal(expectedStrategies.Length, results.Count());
    }
}
