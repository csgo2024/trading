using MediatR;
using Microsoft.AspNetCore.Mvc;
using Trading.API.Application.Commands;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
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
        return Ok(result);
    }
}