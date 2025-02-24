using System.Text;
using Binance.Net.Interfaces.Clients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Trading.API.HostServices;
using Trading.API.Services.Trading.Account;
using Trading.API.Services.Trading.Executors;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.API.Tests.HostServices;

public class TradingServiceTests
{
    private readonly Mock<ILogger<TradingService>> _mockLogger;
    private readonly Mock<AccountProcessorFactory> _accountProcessorFactory;
    private readonly Mock<ExecutorFactory> _executorFactory;
    private readonly Mock<IStrategyRepository> _mockStrategyRepository;
    private readonly Mock<IExecutor> _mockExecutor;
    private readonly Mock<IAccountProcessor> _mockAccountProcessor;
    private readonly Mock<TradingService> _service;
    private readonly CancellationTokenSource _cts;
    private readonly IServiceProvider _serviceProvider;

    public TradingServiceTests()
    {
        _mockLogger = new Mock<ILogger<TradingService>>();
        _mockStrategyRepository = new Mock<IStrategyRepository>();
        _cts = new CancellationTokenSource();
        _mockExecutor = new Mock<IExecutor>();
        _mockAccountProcessor = new Mock<IAccountProcessor>();

        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IExecutor>());
        services.AddSingleton(Mock.Of<IAccountProcessor>());

        // Configure Credential settings
        var credentialSettings = new CredentialSettings
        {
            ApiKey = Encoding.UTF8.GetBytes("test-apikey"),
            ApiSecret = Encoding.UTF8.GetBytes("test-secret")
        };
        services.AddSingleton(Options.Create(credentialSettings));
        services.AddSingleton(Mock.Of<IBinanceRestClient>()); // Add BinanceRestClient mock

        _serviceProvider = services.BuildServiceProvider();
        _accountProcessorFactory = new Mock<AccountProcessorFactory>(_serviceProvider);
        _executorFactory = new Mock<ExecutorFactory>(_serviceProvider);

        _service = new Mock<TradingService>(
            _mockLogger.Object,
            _accountProcessorFactory.Object,
            _mockStrategyRepository.Object,
            _executorFactory.Object)
        {
            CallBase = true
        };

        _service
            .Setup(x => x.SimulateDelay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _executorFactory
            .Setup(f => f.GetExecutor(It.IsAny<StrategyType>()))
            .Returns(_mockExecutor.Object);
        _accountProcessorFactory
            .Setup(f => f.GetAccountProcessor(It.IsAny<AccountType>()))
            .Returns(_mockAccountProcessor.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullStrategies_ShouldContinueExecution()
    {
        // Arrange
        _mockStrategyRepository
            .Setup(x => x.InitializeActiveStrategies())
            .ReturnsAsync(new Dictionary<string, Strategy> { });

        // Act
        Task executeTask = Task.Run(() => _service.Object.StartAsync(_cts.Token));

        await Task.Delay(100); // 让主循环运行 100ms
        _cts.Cancel();  // 立即取消操作，避免长时间等待
        await executeTask; // 等待任务完成

        // Assert
        _executorFactory.Verify(x => x.GetExecutor(It.IsAny<StrategyType>()), Times.Never);
        _accountProcessorFactory.Verify(x => x.GetAccountProcessor(It.IsAny<AccountType>()), Times.Never);
        _service.Verify(service => service.SimulateDelay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        // Cleanup
    }

    [Fact]
    public async Task ExecuteAsync_WithActiveStrategies_ShouldExecuteStrategies()
    {
        // Arrange
        var strategy = new Strategy
        {
            Symbol = "BTCUSDT",
            StrategyType = StrategyType.BuyBottom,
            AccountType = AccountType.Spot,
            Status = StateStatus.Running
        };
        var strategies = new Dictionary<string, Strategy> { { strategy.Symbol, strategy } };

        _mockStrategyRepository
            .Setup(x => x.InitializeActiveStrategies())
            .ReturnsAsync(strategies);
        // Act
        Task executeTask = Task.Run(() => _service.Object.StartAsync(_cts.Token));
        await Task.Delay(100); // 让主循环运行 100ms
        _cts.Cancel();  // 立即取消操作，避免长时间等待
        await executeTask; // 等待任务完成

        // Assert
        _executorFactory.Verify(x => x.GetExecutor(It.IsAny<StrategyType>()), Times.AtLeastOnce);
        _accountProcessorFactory.Verify(x => x.GetAccountProcessor(It.IsAny<AccountType>()), Times.AtLeastOnce);
        _mockExecutor.Verify(
            x => x.Execute(_mockAccountProcessor.Object, strategy, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        // Cleanup
    }

    [Theory]
    [InlineData(true, false)]  // executor is null
    [InlineData(false, true)]  // accountProcessor is null
    [InlineData(false, false)] // both are null
    public async Task ExecuteAsync_WhenProcessorOrExecutorIsNull_ShouldSkipStrategy(
        bool hasExecutor,
        bool hasProcessor)
    {
        // Arrange
        var strategy = new Strategy
        {
            Symbol = "BTCUSDT",
            StrategyType = StrategyType.BuyBottom,
            AccountType = AccountType.Spot,
            Status = StateStatus.Running
        };
        var strategies = new Dictionary<string, Strategy> { { strategy.Symbol, strategy } };

        _mockStrategyRepository
            .Setup(x => x.InitializeActiveStrategies())
            .ReturnsAsync(strategies);

        _executorFactory
            .Setup(x => x.GetExecutor(It.IsAny<StrategyType>()))
            .Returns(hasExecutor ? _mockExecutor.Object : null);

        _accountProcessorFactory
            .Setup(x => x.GetAccountProcessor(It.IsAny<AccountType>()))
            .Returns(hasProcessor ? _mockAccountProcessor.Object : null);

        // Act
        Task executeTask = Task.Run(() => _service.Object.StartAsync(_cts.Token));
        await Task.Delay(100);
        _cts.Cancel();
        await executeTask;

        // Assert

        _mockExecutor.Verify(
            x => x.Execute(_mockAccountProcessor.Object, strategy, It.IsAny<CancellationToken>()),
            Times.Never,
            "Strategy should not be executed when processor or executor is null");

        // Verify the service continues running
        _service.Verify(
            x => x.SimulateDelay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }
}
