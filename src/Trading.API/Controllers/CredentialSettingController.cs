using MediatR;
using Microsoft.AspNetCore.Mvc;
using Trading.Application.Commands;
using Trading.Common.Models;

namespace Trading.API.Controllers;

[ApiController]
[Route("api/v1/credential-setting")]
public class CredentialSettingController : ControllerBase
{
    private readonly IMediator _mediator;

    public CredentialSettingController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Add(CreateCredentialCommand command)
    {
        var result = await _mediator.Send(command);
        var apiResponse = ApiResponse<string>.SuccessResponse(result);
        return Ok(apiResponse);
    }
}
