using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Trading.Application.Helpers;
using Trading.Application.Services.Alarms;
using Trading.Application.Services.Common;
using Trading.Application.Telegram.Handlers;
using Trading.Common.Models;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Telegram.Handlers;

public class StatusCommandHandlerTests
{
    private readonly Mock<IStrategyRepository> _mockStrategyRepository;
    private readonly Mock<IAlarmRepository> _mockAlarmRepository;
    private readonly StatusCommandHandler _handler;

    public StatusCommandHandlerTests()
    {
        // Initialize all mocks
        _mockStrategyRepository = new Mock<IStrategyRepository>();
        _mockAlarmRepository = new Mock<IAlarmRepository>();
        var mockLogger = new Mock<ILogger<StatusCommandHandler>>();
        var alarmLoggerMock = new Mock<ILogger<AlarmNotificationService>>();
        var alarmRepositoryMock = new Mock<IAlarmRepository>();
        var mockBotClient = new Mock<ITelegramBotClient>();

        // Create TelegramSettings
        var telegramSettings = new TelegramSettings { ChatId = "456456481" };
        var options = Options.Create(telegramSettings);

        var jsLoggerMock = new Mock<ILogger<JavaScriptEvaluator>>();
        var taskLoggerMock = new Mock<ILogger<BackgroundTaskManager>>();
        var jsEvaluatorMock = new Mock<JavaScriptEvaluator>(jsLoggerMock.Object);
        var taskManagerMock = new Mock<BackgroundTaskManager>(taskLoggerMock.Object);
        // Create real AlarmNotificationService instance
        var alarmService = new AlarmNotificationService(
            alarmLoggerMock.Object,
            alarmRepositoryMock.Object,
            mockBotClient.Object,
            jsEvaluatorMock.Object,
            taskManagerMock.Object,
            options
        );

        // Create StatusCommandHandler
        _handler = new StatusCommandHandler(
            _mockStrategyRepository.Object,
            _mockAlarmRepository.Object,
            alarmService,
            mockBotClient.Object,
            options,
            mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithActiveStrategies_ShouldSendStatusMessage()
    {
        // Arrange
        var message = new Message { Chat = new Chat { Id = 123 } };
        _mockStrategyRepository.Setup(x => x.GetAllStrategies())
            .ReturnsAsync([new Strategy { Symbol = "BTCUSDT", Status = StateStatus.Running }]);
        _mockAlarmRepository.Setup(x => x.GetAllAlerts())
            .ReturnsAsync([new Alarm() { Symbol = "BTCUSDT", IsActive = true, Expression = "close > 100" }]);

        // Act
        await _handler.HandleAsync("");

    }
}
