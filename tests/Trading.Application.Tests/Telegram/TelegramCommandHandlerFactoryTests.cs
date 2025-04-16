using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Telegram.Bot;
using Trading.Application.Helpers;
using Trading.Application.Services.Alarms;
using Trading.Application.Telegram;
using Trading.Application.Telegram.Handlers;
using Trading.Common.Models;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Telegram;

public class TelegramCommandHandlerFactoryTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TelegramCommandHandlerFactory _factory;

    public TelegramCommandHandlerFactoryTests()
    {
        var services = new ServiceCollection();

        // Add logging services
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        // Configure Telegram settings
        var telegramSettings = new TelegramSettings { ChatId = "test-chat-id" };
        services.AddSingleton(Options.Create(telegramSettings));

        var settings = new TelegramSettings { ChatId = "456456481" };
        var optionsMock = new Mock<IOptions<TelegramSettings>>();
        optionsMock.Setup(x => x.Value).Returns(settings);

        // Add mock dependencies
        services.AddSingleton(Mock.Of<IMediator>());
        services.AddSingleton(Mock.Of<IStrategyRepository>());
        services.AddSingleton(Mock.Of<ICredentialSettingRepository>());
        services.AddSingleton(Mock.Of<IAlarmRepository>());
        services.AddSingleton(Mock.Of<ITelegramBotClient>()); // Add TelegramBotClient mock
        var alarmTaskManagerMock = new Mock<AlarmTaskManager>(Mock.Of<ILogger<AlarmTaskManager>>());
        services.AddSingleton(alarmTaskManagerMock.Object);

        var jsEvaluatorMock = new Mock<JavaScriptEvaluator>(Mock.Of<ILogger<JavaScriptEvaluator>>());
        services.AddSingleton(jsEvaluatorMock.Object);

        var alarmNotificationMock = new Mock<AlarmNotificationService>(
            Mock.Of<ILogger<AlarmNotificationService>>(),
            Mock.Of<IAlarmRepository>(),
            Mock.Of<ITelegramBotClient>(),
            jsEvaluatorMock.Object,
            alarmTaskManagerMock.Object,
            optionsMock.Object
        );
        services.AddSingleton(alarmNotificationMock.Object);

        services.AddTransient<HelpCommandHandler>();
        services.AddTransient<StatusCommandHandler>();
        services.AddTransient<CreateStrategyHandler>();
        services.AddTransient<DeleteStrategyHandler>();
        services.AddTransient<StopStrategyHandler>();
        services.AddTransient<ResumeStrategyHandler>();
        services.AddTransient<AlarmCommandHandler>();

        _serviceProvider = services.BuildServiceProvider();
        _factory = new TelegramCommandHandlerFactory(_serviceProvider);
    }

    [Theory]
    [InlineData("/help", typeof(HelpCommandHandler))]
    [InlineData("/status", typeof(StatusCommandHandler))]
    [InlineData("/create", typeof(CreateStrategyHandler))]
    [InlineData("/delete", typeof(DeleteStrategyHandler))]
    [InlineData("/stop", typeof(StopStrategyHandler))]
    [InlineData("/resume", typeof(ResumeStrategyHandler))]
    [InlineData("/alarm", typeof(AlarmCommandHandler))]
    public void GetHandler_ShouldReturnCorrectHandler(string command, Type expectedType)
    {
        // Act
        var handler = _factory.GetHandler(command);

        // Assert
        Assert.NotNull(handler);
        Assert.IsType(expectedType, handler);
    }

    [Fact]
    public void GetHandler_WithInvalidCommand_ShouldReturnNull()
    {
        // Act
        var handler = _factory.GetHandler("/invalid");

        // Assert
        Assert.Null(handler);
    }
}
