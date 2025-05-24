using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Interfaces.Clients.UsdFuturesApi;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Services.Alerts;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Exchange.Binance.Wrappers.Clients;

namespace Trading.Application.Tests.Services.Alerts;

public class KlineStreamManagerTests
{
    private readonly Mock<ILogger<KlineStreamManager>> _mockLogger;
    private readonly Mock<IMediator> _mockMediator;
    private readonly BinanceSocketClientUsdFuturesApiWrapper _usdFutureSocketClient;
    private readonly KlineStreamManager _manager;
    private readonly CancellationTokenSource _cts;

    private readonly Mock<IBinanceSocketClientUsdFuturesApiAccount> _mockAccount;
    private readonly Mock<IBinanceSocketClientUsdFuturesApiTrading> _mockTrading;
    private readonly Mock<IBinanceSocketClientUsdFuturesApiExchangeData> _mockExchangeData;

    public KlineStreamManagerTests()
    {
        _mockLogger = new Mock<ILogger<KlineStreamManager>>();
        _mockMediator = new Mock<IMediator>();
        _cts = new CancellationTokenSource();

        _mockAccount = new Mock<IBinanceSocketClientUsdFuturesApiAccount>();
        _mockTrading = new Mock<IBinanceSocketClientUsdFuturesApiTrading>();
        _mockExchangeData = new Mock<IBinanceSocketClientUsdFuturesApiExchangeData>();

        _usdFutureSocketClient = new BinanceSocketClientUsdFuturesApiWrapper(
            _mockAccount.Object,
            _mockExchangeData.Object,
            _mockTrading.Object);

        _manager = new KlineStreamManager(
            _mockLogger.Object,
            _mockMediator.Object,
            _usdFutureSocketClient);
    }

    [Fact]
    public async Task SubscribeSymbols_WithValidInput_ShouldSubscribeSuccessfully()
    {
        // Arrange
        var symbols = new HashSet<string> { "BTCUSDT" };
        var intervals = new HashSet<string> { "5m" };

        _mockExchangeData
            .Setup(x => x.SubscribeToKlineUpdatesAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<KlineInterval>>(),
                It.IsAny<Action<DataEvent<IBinanceStreamKlineData>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallResult<UpdateSubscription>(null, null, null));

        // Act
        var result = await _manager.SubscribeSymbols(symbols, intervals, _cts.Token);

        // Assert
        Assert.True(result);
        _mockExchangeData.Verify(
            x => x.SubscribeToKlineUpdatesAsync(
                It.Is<IEnumerable<string>>(s => s.Contains("BTCUSDT")),
                It.Is<IEnumerable<KlineInterval>>(i => i.Contains(KlineInterval.FiveMinutes)),
                It.IsAny<Action<DataEvent<IBinanceStreamKlineData>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeSymbols_WithEmptyInput_ShouldReturnFalse()
    {
        // Arrange
        var emptySymbols = new HashSet<string>();
        var intervals = new HashSet<string> { "1m" };

        // Act
        var result = await _manager.SubscribeSymbols(emptySymbols, intervals, _cts.Token);

        // Assert
        Assert.False(result);
        _mockExchangeData.Verify(
            x => x.SubscribeToKlineUpdatesAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<KlineInterval>>(),
                It.IsAny<Action<DataEvent<IBinanceStreamKlineData>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SubscribeSymbols_WhenSubscriptionFails_ShouldReturnFalse()
    {
        // Arrange
        var symbols = new HashSet<string> { "BTCUSDT" };
        var intervals = new HashSet<string> { "5m" };

        _mockExchangeData
            .Setup(x => x.SubscribeToKlineUpdatesAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<KlineInterval>>(),
                It.IsAny<Action<DataEvent<IBinanceStreamKlineData>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallResult<UpdateSubscription>(null, null, new CantConnectError()));

        // Act
        var result = await _manager.SubscribeSymbols(symbols, intervals, _cts.Token);

        // Assert
        Assert.False(result);
        _mockLogger.VerifyLoggingOnce(LogLevel.Error, "");
    }

    [Fact]
    public async Task Handle_AlertCreatedEvent_ShouldUpdateSubscriptions()
    {
        // Arrange
        var alert = new Alert { Symbol = "ETHUSDT", Interval = "1h" };
        var notification = new AlertCreatedEvent(alert);

        _mockExchangeData
            .Setup(x => x.SubscribeToKlineUpdatesAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<KlineInterval>>(),
                It.IsAny<Action<DataEvent<IBinanceStreamKlineData>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallResult<UpdateSubscription>(null, null, null));

        // Act
        await _manager.Handle(notification, _cts.Token);

        // Assert
        _mockExchangeData.Verify(
            x => x.SubscribeToKlineUpdatesAsync(
                It.Is<IEnumerable<string>>(s => s.Contains("ETHUSDT")),
                It.Is<IEnumerable<KlineInterval>>(i => i.Contains(KlineInterval.OneHour)),
                It.IsAny<Action<DataEvent<IBinanceStreamKlineData>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AlertResumedEvent_ShouldUpdateSubscriptions()
    {
        // Arrange
        var alert = new Alert { Symbol = "ETHUSDT", Interval = "1h" };
        var notification = new AlertResumedEvent(alert);

        _mockExchangeData
            .Setup(x => x.SubscribeToKlineUpdatesAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<KlineInterval>>(),
                It.IsAny<Action<DataEvent<IBinanceStreamKlineData>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallResult<UpdateSubscription>(null, null, null));

        // Act
        await _manager.Handle(notification, _cts.Token);

        // Assert
        _mockExchangeData.Verify(
            x => x.SubscribeToKlineUpdatesAsync(
                It.Is<IEnumerable<string>>(s => s.Contains("ETHUSDT")),
                It.Is<IEnumerable<KlineInterval>>(i => i.Contains(KlineInterval.OneHour)),
                It.IsAny<Action<DataEvent<IBinanceStreamKlineData>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void NeedsReconnection_AfterReconnectInterval_ShouldReturnTrue()
    {
        // Arrange
        var lastConnectionTime = DateTime.UtcNow.AddHours(-13);
        var field = typeof(KlineStreamManager).GetField("_lastConnectionTime",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(_manager, lastConnectionTime);

        // Act
        var result = _manager.NeedsReconnection();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void NeedsReconnection_BeforeReconnectInterval_ShouldReturnFalse()
    {
        // Arrange
        var lastConnectionTime = DateTime.UtcNow.AddHours(-1);
        var field = typeof(KlineStreamManager).GetField("_lastConnectionTime",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(_manager, lastConnectionTime);

        // Act
        var result = _manager.NeedsReconnection();

        // Assert
        Assert.False(result);
    }

}
