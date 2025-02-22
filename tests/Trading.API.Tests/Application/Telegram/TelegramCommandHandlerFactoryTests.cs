using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Telegram.Bot;
using Trading.API.Application.Telegram;
using Trading.API.Application.Telegram.Handlers;
using Trading.Common.Models;
using Trading.Domain.IRepositories;
using Xunit;

namespace Trading.API.Tests.Application.Telegram;

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
        
        // Add mock dependencies
        services.AddSingleton<IMediator>(Mock.Of<IMediator>());
        services.AddSingleton<IStrategyRepository>(Mock.Of<IStrategyRepository>());
        services.AddSingleton<ICredentialSettingRepository>(Mock.Of<ICredentialSettingRepository>());
        services.AddSingleton<ITelegramBotClient>(Mock.Of<ITelegramBotClient>()); // Add TelegramBotClient mock

        services.AddTransient<StartCommandHandler>();
        services.AddTransient<StatusCommandHandler>();
        services.AddTransient<CreateStrategyHandler>();
        services.AddTransient<DeleteStrategyHandler>();
        services.AddTransient<StopStrategyHandler>();
        services.AddTransient<ResumeStrategyHandler>();
        
        _serviceProvider = services.BuildServiceProvider();
        _factory = new TelegramCommandHandlerFactory(_serviceProvider);
    }

    [Theory]
    [InlineData("/start", typeof(StartCommandHandler))]
    [InlineData("/status", typeof(StatusCommandHandler))]
    [InlineData("/create", typeof(CreateStrategyHandler))]
    [InlineData("/delete", typeof(DeleteStrategyHandler))]
    [InlineData("/stop", typeof(StopStrategyHandler))]
    [InlineData("/resume", typeof(ResumeStrategyHandler))]
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