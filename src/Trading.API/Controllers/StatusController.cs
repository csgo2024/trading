using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Trading.Common.Models;
using Trading.Exchange.Binance.Wrappers.Clients;
using Trading.Infrastructure;

namespace Trading.API.Controllers;

[ApiController]
[Route("api/v1/status")]
public class StatusController : ControllerBase
{
    private readonly IMongoDbContext _mongoDbContext;
    private readonly MongoDbSettings _settings;

    private readonly BinanceRestClientSpotApiWrapper _spotApiRestClient;
    private readonly BinanceRestClientUsdFuturesApiWrapper _usdFutureApiRestClient;
    private readonly BinanceRestClientWrapper _restClient;
    private readonly BinanceSocketClientSpotApiWrapper _spotApiSocketClient;
    private readonly BinanceSocketClientUsdFuturesApiWrapper _usdFutureApiSocketClient;
    private readonly BinanceSocketClientWrapper _socketClient;
    public StatusController(IMongoDbContext mongoDbContext,
                            BinanceRestClientSpotApiWrapper spotApiRestClient,
                            BinanceRestClientUsdFuturesApiWrapper usdFutureApiRestClient,
                            BinanceRestClientWrapper restClient,
                            BinanceSocketClientSpotApiWrapper spotApiSocketClient,
                            BinanceSocketClientUsdFuturesApiWrapper usdFutureApiSocketClient,
                            BinanceSocketClientWrapper socketClient,
                            IOptions<MongoDbSettings> settings)
    {
        _mongoDbContext = mongoDbContext;
        _settings = settings.Value;
        _spotApiRestClient = spotApiRestClient;
        _usdFutureApiRestClient = usdFutureApiRestClient;
        _restClient = restClient;
        _spotApiSocketClient = spotApiSocketClient;
        _usdFutureApiSocketClient = usdFutureApiSocketClient;
        _socketClient = socketClient;
    }

    [HttpGet("")]
    public async Task<IActionResult> GetAll()
    {
        ArgumentNullException.ThrowIfNull(_spotApiRestClient.Account);
        ArgumentNullException.ThrowIfNull(_spotApiRestClient.ExchangeData);
        ArgumentNullException.ThrowIfNull(_spotApiRestClient.Trading);
        ArgumentNullException.ThrowIfNull(_usdFutureApiRestClient.Account);
        ArgumentNullException.ThrowIfNull(_usdFutureApiRestClient.ExchangeData);
        ArgumentNullException.ThrowIfNull(_usdFutureApiRestClient.Trading);
        ArgumentNullException.ThrowIfNull(_restClient.SpotApi);
        ArgumentNullException.ThrowIfNull(_restClient.UsdFuturesApi);
        ArgumentNullException.ThrowIfNull(_spotApiSocketClient.Account);
        ArgumentNullException.ThrowIfNull(_spotApiSocketClient.ExchangeData);
        ArgumentNullException.ThrowIfNull(_spotApiSocketClient.Trading);
        ArgumentNullException.ThrowIfNull(_usdFutureApiSocketClient.Account);
        ArgumentNullException.ThrowIfNull(_usdFutureApiSocketClient.ExchangeData);
        ArgumentNullException.ThrowIfNull(_usdFutureApiSocketClient.Trading);
        ArgumentNullException.ThrowIfNull(_socketClient.SpotApi);
        ArgumentNullException.ThrowIfNull(_socketClient.UsdFuturesApi);
        _ = await _mongoDbContext.Ping();
        return Ok(_settings);
    }
}
