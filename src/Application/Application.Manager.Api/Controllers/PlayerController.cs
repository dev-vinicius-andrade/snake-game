using Application.Manager.Api.Hubs;
using Library.Commons.Api.Attributes.Filters;
using Library.Commons.Api.Entities.Requests.Player;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Application.Manager.Api.Controllers;
[Route("[controller]")]
[ApiController]

public class PlayerController : Controller
{
    private readonly IHubContext<ManagerHub> _hubContext;
    private readonly ILogger<PlayerController> _logger;

    public PlayerController(IHubContext<ManagerHub> hubContext,ILogger<PlayerController> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    [HttpPost("{id}/joined")]
    [ApiKeyAuth]
    public async Task<IActionResult> Joined(string id,PlayerJoinedRequest request,CancellationToken cancellationToken=default)
    {
        _logger.LogInformation($"Player joined room {id} {request.Nickname} {request.GameServerUrl}" );

        await _hubContext.Clients.Client(id).SendAsync("onJoinedRoom", request, cancellationToken: cancellationToken);
        _logger.LogInformation($"Player notified joined room {id} {request.Nickname} {request.GameServerUrl}");
        return Ok();
    }
}