using MediatR;
using Microsoft.AspNetCore.Mvc;
using Trading.Application.Commands;
using Trading.Application.Queries;
using Trading.Common.Enums;
using Trading.Common.Models;
using Trading.Domain.Entities;

namespace Trading.API.Controllers;

[ApiController]
[Route("api/v1/strategy")]
public class StrategyController : ControllerBase
{

    private readonly IMediator _mediator;
    private readonly IStrategyQuery _strategyQuery;

    public StrategyController(IStrategyQuery strategyQuery, IMediator mediator)
    {
        _mediator = mediator;
        _strategyQuery = strategyQuery;
    }

    [HttpGet("")]
    public async Task<IActionResult> GetStrategyList([FromQuery] PagedRequest request, AccountType type = AccountType.Spot)
    {
        var strategy = await _strategyQuery.GetStrategyListAsync(request);
        var apiResponse = ApiResponse<PagedResult<Strategy>>.SuccessResponse(strategy);
        return Ok(apiResponse);
    }

    [HttpPost("")]
    public async Task<IActionResult> AddStrategy([FromBody] CreateStrategyCommand command)
    {
        var result = await _mediator.Send(command);
        var apiResponse = ApiResponse<Strategy>.SuccessResponse(result);
        return Ok(apiResponse);
    }
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteStrategy(string id)
    {
        var command = new DeleteStrategyCommand() { Id = id };
        var result = await _mediator.Send(command);
        var apiResponse = ApiResponse<bool>.SuccessResponse(result);
        return Ok(apiResponse);
    }
    [HttpGet("{id}")]
    public async Task<IActionResult> GetStrategyById(string id)
    {
        var data = await _strategyQuery.GetStrategyByIdAsync(id);
        if (data == null)
        {
            return NotFound();
        }
        return Ok(ApiResponse<Strategy>.SuccessResponse(data));
    }
}
