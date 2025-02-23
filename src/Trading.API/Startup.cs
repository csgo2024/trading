using System.Text;
using Binance.Net.Clients;
using CryptoExchange.Net.Authentication;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Trading.API.Application.Commands;
using Trading.API.Application.Logging;
using Trading.API.Application.Middlerwares;
using Trading.API.Application.Queries;
using Trading.API.Application.Telegram;
using Trading.API.Application.Telegram.Handlers;
using Trading.API.Extensions;
using Trading.API.HostServices;
using Trading.API.Services.Trading.Account;
using Trading.Common.Models;
using Trading.Domain.Entities;
using Trading.Infrastructure;

namespace Trading.API;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TelegramSettings>(Configuration.GetSection("TelegramSettings"));
        services.Configure<CredentialSettings>(Configuration.GetSection("CredentialSettings"));
        services.Configure<string>(Configuration.GetSection("PrivateKey"));

        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll",
                builder =>
                {
                    builder
                        .AllowAnyOrigin()        // 允许任何来源
                        .AllowAnyMethod()        // 允许任何HTTP方法
                        .AllowAnyHeader();       // 允许任何请求头
                }
            );
        });

        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        services.AddMongoDb(Configuration);
        services.AddSingleton<StartCommandHandler>();
        services.AddSingleton<StatusCommandHandler>();
        services.AddSingleton<CreateStrategyHandler>();
        services.AddSingleton<DeleteStrategyHandler>();
        services.AddSingleton<StopStrategyHandler>();
        services.AddSingleton<ResumeStrategyHandler>();
        services.AddSingleton<TelegramCommandHandlerFactory>();
        services.AddSingleton<ITelegramCommandHandler, TelegramCommandHandler>();

        services.AddSingleton<IErrorMessageResolver, DefaultErrorMessageResolver>();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddConsole(); // 控制台日志
            builder.AddDebug();   // Debug日志
            builder.AddTelegramLogger(); // Telegram日志

            // 配置最小日志级别
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddScoped<IStrategyQuery, StrategyQuery>();
        services.AddSingleton<ICredentialQuery, CredentialQuery>();

        services.AddSingleton(provider =>
        {
            var privateKey = Configuration.GetSection("PrivateKey")?.Value ?? string.Empty;
            var query = provider.GetRequiredService<ICredentialQuery>();
            var settings = query.GetCredential();
            var apiKey = "your-api-key";
            if (settings?.ApiKey != null)
            {
                apiKey = Encoding.UTF8.GetString(CreateCredentialCommandHandler.DecryptData(settings.ApiKey, privateKey));
            }
            var apiSecret = "your-secret";
            if (settings?.ApiSecret != null)
            {
                apiSecret = Encoding.UTF8.GetString(CreateCredentialCommandHandler.DecryptData(settings.ApiSecret, privateKey));
            }
            var restClient = new BinanceRestClient(options =>
            {
                options.ApiCredentials = new ApiCredentials(apiKey, apiSecret);
            });
            return restClient;
        });

        services.AddSingleton<ITelegramBotClient>(provider =>
        {
            var settings = provider.GetRequiredService<IOptions<TelegramSettings>>().Value;
            return new TelegramBotClient(settings.BotToken);
        });

        services.AddSingleton<BinanceSpotRestClientWrapper>(provider =>
        {
            var binanceRestClient = provider.GetRequiredService<BinanceRestClient>();
            return new BinanceSpotRestClientWrapper(binanceRestClient.SpotApi.Trading,
                                                    binanceRestClient.SpotApi.ExchangeData);
        });
        services.AddSingleton<BinanceFeatureRestClientWrapper>(provider =>
        {
            var binanceRestClient = provider.GetRequiredService<BinanceRestClient>();
            return new BinanceFeatureRestClientWrapper(binanceRestClient.UsdFuturesApi.Trading,
                                                       binanceRestClient.UsdFuturesApi.ExchangeData);
        });


        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining(typeof(Program));

        });
        services.AddHostedService<TelegramBotService>();
        services.AddHostedService<TradingService>();
        services.AddTradingServices();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        app.UseCors("AllowAll");  // 使用允许所有的策略
        app.UseExceptionHandlingMiddleware();
        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseRouting();

        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}