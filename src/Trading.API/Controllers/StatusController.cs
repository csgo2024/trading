using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Trading.Common.Models;
using Trading.Infrastructure;

namespace Trading.API.Controllers;

[ApiController]
[Route("[controller]")]
public class StatusController : ControllerBase
{
    private readonly  IMongoDbContext _mongoDbContext;
    private readonly  MongoDbSettings _settings;

    public StatusController(IMongoDbContext mongoDbContext,IOptions<MongoDbSettings> settings)
    {
        _mongoDbContext = mongoDbContext;
        _settings = settings.Value;
    }

    [HttpGet("")]
    public async Task<IActionResult> GetAll()
    {
        var status =  await _mongoDbContext.Ping();

        return Ok(_settings);
    }
}