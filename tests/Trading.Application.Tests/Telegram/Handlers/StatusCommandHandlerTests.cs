using Microsoft.Extensions.Logging;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Trading.Application.Telegram.Handlers;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Telegram.Handlers;

public class StatusCommandHandlerTests
{
    private readonly Mock<ITelegramBotClient> _mockBotClient;
    private readonly Mock<IStrategyRepository> _mockStrategyRepository;
    private readonly Mock<ILogger<StatusCommandHandler>> _mockLogger;
    private readonly StatusCommandHandler _handler;

    public StatusCommandHandlerTests()
    {
        _mockBotClient = new Mock<ITelegramBotClient>();
        _mockStrategyRepository = new Mock<IStrategyRepository>();
        _mockLogger = new Mock<ILogger<StatusCommandHandler>>();
        _handler = new StatusCommandHandler(_mockStrategyRepository.Object, _mockLogger.Object);
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
