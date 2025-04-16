using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Trading.Application.Helpers;
using Trading.Application.Services.Alarms;
using Trading.Application.Telegram.Handlers;
using Trading.Common.Models;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Telegram.Handlers;

public class StatusCommandHandlerTests
{
    private readonly Mock<IStrategyRepository> _mockStrategyRepository;
    private readonly Mock<ILogger<StatusCommandHandler>> _mockLogger;
    private readonly Mock<ILogger<AlarmNotificationService>> _alarmLoggerMock;
    private readonly Mock<IAlarmRepository> _alarmRepositoryMock;
    private readonly Mock<ITelegramBotClient> _mockBotClient;
    private readonly Mock<AlarmTaskManager> _taskManagerMock;
    private readonly AlarmNotificationService _alarmService;
    private readonly StatusCommandHandler _handler;
    private readonly Mock<ILogger<JavaScriptEvaluator>> _jsLoggerMock;
    private readonly Mock<JavaScriptEvaluator> _jsEvaluatorMock;
    private readonly Mock<ILogger<AlarmTaskManager>> _alarmTaskLoggerMock;
    private readonly Mock<AlarmTaskManager> _alarmTaskManagerMock;
    public StatusCommandHandlerTests()
    {
        // Initialize all mocks
        _mockStrategyRepository = new Mock<IStrategyRepository>();
        _mockLogger = new Mock<ILogger<StatusCommandHandler>>();
        _alarmLoggerMock = new Mock<ILogger<AlarmNotificationService>>();
        _alarmRepositoryMock = new Mock<IAlarmRepository>();
        _mockBotClient = new Mock<ITelegramBotClient>();
        _taskManagerMock = new Mock<AlarmTaskManager>();

        // Create TelegramSettings
        var telegramSettings = new TelegramSettings { ChatId = "test-chat-id" };
        var options = Options.Create(telegramSettings);

        _jsLoggerMock = new Mock<ILogger<JavaScriptEvaluator>>();
        _alarmTaskLoggerMock = new Mock<ILogger<AlarmTaskManager>>();
        _jsEvaluatorMock = new Mock<JavaScriptEvaluator>(_jsLoggerMock.Object);
        _alarmTaskManagerMock = new Mock<AlarmTaskManager>(_alarmTaskLoggerMock.Object);
        // Create real AlarmNotificationService instance
        _alarmService = new AlarmNotificationService(
            _alarmLoggerMock.Object,
            _alarmRepositoryMock.Object,
            _mockBotClient.Object,
            _jsEvaluatorMock.Object,
            _alarmTaskManagerMock.Object,
            options
        );

        // Create StatusCommandHandler
        _handler = new StatusCommandHandler(
            _mockStrategyRepository.Object,
            _alarmService,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithActiveStrategies_ShouldSendStatusMessage()
    {
        // Arrange
        var message = new Message { Chat = new Chat { Id = 123 } };
        _mockStrategyRepository.Setup(x => x.GetAllStrategies())
            .ReturnsAsync(new List<Strategy>
            {
                new Strategy { Symbol = "BTCUSDT", Status = StateStatus.Running }
            });

        // Act
        await _handler.HandleAsync("");

    }
}
