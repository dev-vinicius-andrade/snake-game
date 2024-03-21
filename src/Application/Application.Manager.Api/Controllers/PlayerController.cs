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

    public PlayerController(IHubContext<ManagerHub> hubContext)
    {
        _hubContext = hubContext;
    }

    // GET
    [HttpPost("{id}/joined")]
    [ApiKeyAuth]
    public async Task<IActionResult> Joined(string id,PlayerJoinedRequest request,CancellationToken cancellationToken=default)
    {
        await _hubContext.Clients.Client(id).SendAsync("onJoinedRoom", request, cancellationToken: cancellationToken);
        return Ok();
    }
}