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
    private readonly BinanceRestClientUsdFuturesApiWrapper _binanceFutureRestClientWrapper;
    private readonly BinanceRestClientSpotApiWrapper _binanceSpotRestClientWrapper;

    public StatusController(IMongoDbContext mongoDbContext,
        BinanceRestClientUsdFuturesApiWrapper futureRestClientWrapper,
        BinanceRestClientSpotApiWrapper spotRestClientWrapper,
        IOptions<MongoDbSettings> settings)
    {
        _mongoDbContext = mongoDbContext;
        _settings = settings.Value;
        _binanceFutureRestClientWrapper = futureRestClientWrapper;
        _binanceSpotRestClientWrapper = spotRestClientWrapper;
    }

    [HttpGet("")]
    public async Task<IActionResult> GetAll()
    {
        _ = await _mongoDbContext.Ping();
        if (_binanceSpotRestClientWrapper == null)
        {
            throw new InvalidOperationException("Binance Spots not found");
        }

        if (_binanceFutureRestClientWrapper == null)
        {
            throw new InvalidOperationException("Binance Futures not found");
        }
        return Ok(_settings);
    }
}
