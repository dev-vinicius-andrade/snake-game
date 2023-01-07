using Application.Game.Server.Services;
using Domain.Game.Entities;
using Library.Commons.Api.Entities.Response;
using Library.Commons.Game.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Domain.Game.ViewModels.Request.Player;

namespace Application.Game.Server.Controllers;
[Route("[controller]")]
[ApiController]
[AllowAnonymous]
public class PlayerController : Controller
{
    private readonly IPlayerService<Player> _playerService;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public PlayerController(IPlayerService<Player> playerService, JsonSerializerOptions jsonSerializerOptions)
    {
        _playerService = playerService;
        _jsonSerializerOptions = jsonSerializerOptions;
    }
    [HttpPost]
    public async Task<IActionResult> Create(AddPlayerRequest request, CancellationToken cancellationToken = default)
    {
        var player = await Task.FromResult(_playerService.Create(request.Name));
        return Json(new ObjectResponseModel<Player> { Data = player });
    }
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var player = await Task.FromResult(_playerService.Get(id));
        if (player == null) return NoContent();
        return Json(new ObjectResponseModel<Player> { Data = player });
    }
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
    {
        var players = await Task.FromResult(_playerService.GetAll());
        if (players == null) return NoContent();
        return Json(new ObjectResponseModel<IEnumerable<Player>> { Data = players });
    }
}