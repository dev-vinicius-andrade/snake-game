using System.Text.Json.Serialization;
using Application.Manager.Api.Hubs;
using Application.Manager.Api.Rooms;
using Library.Commons.Api.Attributes.Filters;
using Library.Commons.Api.Entities.Requests.Room;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Application.Manager.Api.Controllers;

public class CreateRoomRequest
{
    /// <summary>The caller's connection to the manager hub, where the answer is sent.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("maxPlayers")]
    public int MaxPlayers { get; set; }
}

[Route("[controller]")]
[ApiController]
public class RoomsController(RoomCreationService creation, RoomRegistry registry, IHubContext<ManagerHub> hub) : Controller
{
    [HttpPost]
    [AllowAnonymous]
    public IActionResult Create(CreateRoomRequest request)
    {
        var result = creation.Request(request.Id, request.Name, request.MaxPlayers);
        return result.Success ? Accepted(new { code = result.Code }) : BadRequest(new { error = result.Error });
    }

    /// <summary>What the invite page shows before joining.</summary>
    [HttpGet("code/{code}")]
    [AllowAnonymous]
    public IActionResult ByCode(string code)
    {
        var room = registry.FindByCode(code);
        if (room is null) return NotFound();
        var summary = room.Summary;
        return Ok(new { id = summary.Id, code = summary.Code, name = summary.Name, players = summary.Players, maxPlayers = summary.MaxPlayers });
    }

    [HttpPost("{requesterId}/created")]
    [ApiKeyAuth]
    public async Task<IActionResult> Created(string requesterId, RoomCreatedRequest request, CancellationToken cancellationToken = default)
    {
        registry.Register(request);
        await hub.Clients.Client(requesterId).SendAsync("onRoomCreated",
            new { roomId = request.RoomId, code = request.Code, name = request.Name, maxPlayers = request.MaxPlayers }, cancellationToken);
        return Ok();
    }

    [HttpPost("{requesterId}/failed")]
    [ApiKeyAuth]
    public async Task<IActionResult> Failed(string requesterId, FailureRequest request, CancellationToken cancellationToken = default)
    {
        await hub.Clients.Client(requesterId).SendAsync("onRoomCreateFailed", new { reason = request.Reason }, cancellationToken);
        return Ok();
    }

    [HttpPost("heartbeat")]
    [ApiKeyAuth]
    public IActionResult Heartbeat(RoomHeartbeatRequest request)
    {
        registry.Update(request);
        return Ok();
    }
}

[Route("admin/rooms")]
[ApiController]
[AdminKeyAuth]
public class AdminRoomsController(RoomAdminService admin) : Controller
{
    [HttpGet]
    public IActionResult List() => Ok(admin.List().Select(room => new
    {
        id = room.Summary.Id,
        code = room.Summary.Code,
        name = room.Summary.Name,
        isPrivate = room.Summary.IsPrivate,
        players = room.Summary.Players,
        maxPlayers = room.Summary.MaxPlayers,
        createdAt = room.Summary.CreatedAt,
        server = room.BaseUrl,
    }));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
        => await admin.DeleteAsync(id, cancellationToken) switch
        {
            DeleteResult.Deleted => NoContent(),
            DeleteResult.NotFound => NotFound(),
            _ => StatusCode(StatusCodes.Status502BadGateway, new { error = "serverUnreachable" }),
        };
}
