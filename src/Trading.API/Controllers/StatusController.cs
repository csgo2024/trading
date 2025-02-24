using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Trading.API.Services.Trading.Account;
using Trading.Common.Models;
using Trading.Infrastructure;

namespace Trading.API.Controllers;

[ApiController]
[Route("[controller]")]
public class StatusController : ControllerBase
{
    private readonly IMongoDbContext _mongoDbContext;
    private readonly MongoDbSettings _settings;
    private readonly BinanceFeatureRestClientWrapper _binanceFeatureRestClientWrapper;
    private readonly BinanceSpotRestClientWrapper _binanceSpotRestClientWrapper;

    public StatusController(IMongoDbContext mongoDbContext,
        BinanceFeatureRestClientWrapper featureRestClientWrapper,
        BinanceSpotRestClientWrapper spotRestClientWrapper,
        IOptions<MongoDbSettings> settings)
    {
        _mongoDbContext = mongoDbContext;
        _settings = settings.Value;
        _binanceFeatureRestClientWrapper = featureRestClientWrapper;
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

        if (_binanceFeatureRestClientWrapper == null)
        {
            throw new InvalidOperationException("Binance Features not found");
        }
        return Ok(_settings);
    }
}
