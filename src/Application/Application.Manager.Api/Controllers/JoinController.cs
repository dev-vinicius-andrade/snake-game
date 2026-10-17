using System.Net;
using System.Text.Json;
using Application.Manager.Api.Hubs;
using Application.Manager.Api.Rooms;
using Library.Commons.Api.Attributes.Filters;
using Library.Commons.Api.Entities.Requests.Join;
using Library.Commons.Api.Entities.Requests.Player;
using Library.Commons.Api.Entities.Response;
using Library.Commons.Eventbus.RabbitMq.Interfaces;
using Library.Commons.Game.Domain.Constants;
using Library.Commons.Game.Domain.Entities.Eventbus.Payloads;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;

namespace Application.Manager.Api.Controllers;
[Route("[controller]")]
[ApiController]
[AllowAnonymous]
public class JoinController : Controller
{
    private readonly RoomRegistry _registry;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly IEventbusPublisher<IBasicProperties> _publisher;
    private readonly IHubContext<ManagerHub> _hubContext;

    public JoinController(JsonSerializerOptions jsonSerializerOptions, IEventbusPublisher<IBasicProperties> publisher, IHubContext<ManagerHub> hubContext, RoomRegistry registry)
    {
        _registry = registry;
        _jsonSerializerOptions = jsonSerializerOptions;
        _publisher = publisher;
        _hubContext = hubContext;
    }
    [HttpPost]
    public async Task<IActionResult> Join(JoinRequest request,  CancellationToken cancellationToken = default)
    {
        try
        {
            PublishJoinEvent(request);
            return Ok();

        }
        catch (Exception ex)
        {

            return BadRequest(new ErrorResponseModel(HttpStatusCode.BadRequest, ex));
        }
    }


    // GET
    [HttpPost("{id}")]
    [ApiKeyAuth]
    public async Task<IActionResult> Join(string id, PlayerJoinedRequest request, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Client(id).SendAsync("onJoin", request, cancellationToken: cancellationToken);
        return Ok();
    }

    private void PublishJoinEvent(JoinRequest request,Guid? roomId =null)
    {
        _publisher.Publish(new PlayerJoinPayload
        {
            Id = request.Id,
            Nickname = request.Nickname,
            RoomId = roomId
        }, EventNames.PlayerJoinRequest);
    }

    [HttpPost("room/{id:guid}")]
    public async Task<IActionResult> Join(Guid id, JoinRequest request, CancellationToken cancellationToken = default)
    {
        if (_registry.Get(id) is null) return NotFound();
        try
        {
            PublishJoinEvent(request, id);
            return Ok();

        }
        catch (Exception ex)
        {

            return BadRequest(new ErrorResponseModel(HttpStatusCode.BadRequest,ex) );
        }
        
    }

    /// <summary>Joins a room by its invite code.</summary>
    [HttpPost("code/{code}")]
    public IActionResult JoinByCode(string code, JoinRequest request)
    {
        var room = _registry.FindByCode(code);
        if (room is null) return NotFound();
        try
        {
            PublishJoinEvent(request, room.Summary.Id);
            return Ok(new { roomId = room.Summary.Id });
        }
        catch (Exception ex)
        {
            return BadRequest(new ErrorResponseModel(HttpStatusCode.BadRequest, ex));
        }
    }
}
