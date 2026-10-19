using System.Net;
using System.Text.Json;
using Application.Manager.Api.Accounts;
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

/// <summary>
/// Asks the game servers for a place for a player. An optional <c>Authorization: Bearer &lt;device token&gt;</c> joins as that account
/// (docs/ACHIEVEMENTS.md): its display name replaces the nickname. A header that is not a valid token is 401 invalidToken; a guest nickname
/// that looks like an account (ends in '#' and digits) is 400 invalidNickname.
/// </summary>
[Route("[controller]")]
[ApiController]
[AllowAnonymous]
public class JoinController : Controller
{
    private readonly RoomRegistry _registry;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly IEventbusPublisher<IBasicProperties> _publisher;
    private readonly IHubContext<ManagerHub> _hubContext;
    private readonly AccountService _accounts;

    public JoinController(JsonSerializerOptions jsonSerializerOptions, IEventbusPublisher<IBasicProperties> publisher, IHubContext<ManagerHub> hubContext,
        RoomRegistry registry, AccountService accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        _registry = registry;
        _jsonSerializerOptions = jsonSerializerOptions;
        _publisher = publisher;
        _hubContext = hubContext;
        _accounts = accounts;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status401Unauthorized)]
    public IActionResult Join(JoinRequest request)
        => PublishJoin(request, null, Ok);

    // GET
    [HttpPost("{id}")]
    [ApiKeyAuth]
    public async Task<IActionResult> Join(string id, PlayerJoinedRequest request, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Client(id).SendAsync("onJoin", request, cancellationToken: cancellationToken);
        return Ok();
    }

    [HttpPost("room/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Join(Guid id, JoinRequest request)
        => _registry.Get(id) is null ? NotFound() : PublishJoin(request, id, Ok);

    /// <summary>Joins a room by its invite code.</summary>
    [HttpPost("code/{code}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult JoinByCode(string code, JoinRequest request)
    {
        var room = _registry.FindByCode(code);
        return room is null ? NotFound() : PublishJoin(request, room.Summary.Id, () => Ok(new { roomId = room.Summary.Id }));
    }

    private IActionResult PublishJoin(JoinRequest request, Guid? roomId, Func<IActionResult> answer)
    {
        var identity = JoinIdentities.Resolve(_accounts, Request.Headers.Authorization.ToString(), request.Nickname);
        if (!identity.Succeeded)
            return Refusal(identity.Error);
        try
        {
            PublishJoinEvent(request.Id, identity.Value, roomId);
            return answer();
        }
        catch (Exception ex)
        {
            return BadRequest(new ErrorResponseModel(HttpStatusCode.BadRequest, ex));
        }
    }

    private void PublishJoinEvent(string connectionId, JoinIdentity identity, Guid? roomId)
    {
        _publisher.Publish(new PlayerJoinPayload
        {
            Id = connectionId,
            Nickname = identity.Nickname,
            RoomId = roomId,
            AccountId = identity.AccountId,
        }, EventNames.PlayerJoinRequest);
    }

    private ObjectResult Refusal(string error)
        => StatusCode(error == AccountErrors.InvalidToken ? StatusCodes.Status401Unauthorized : StatusCodes.Status400BadRequest,
            new AccountErrorResponse { Error = error });
}
