using Library.Commons.Api.Entities.Response;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Domain.Game.Entities;

namespace Application.Game.Server.Controllers;
[Route("[controller]")]
[ApiController]
[AllowAnonymous]
public class RoomsController : Controller
{
    private readonly IRoomsService<Room> _roomsService;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public RoomsController(IRoomsService<Room> roomsService, JsonSerializerOptions jsonSerializerOptions)
    {
        _roomsService = roomsService;
        _jsonSerializerOptions = jsonSerializerOptions;
    }

    [HttpGet("available")]
    public async Task<IActionResult> Available(CancellationToken cancellationToken=default)
    {
        var availableRooms= await Task.FromResult(_roomsService.GetAvailableRooms().ToList());
        if (!availableRooms.Any()) return NoContent();
        return Json(new ObjectResponseModel<IEnumerable<Room>>{Data = availableRooms });
    }
    [HttpPost]
    public async Task<IActionResult> Create(CancellationToken cancellationToken = default)
    {
        var room = await Task.FromResult(_roomsService.CreateRoom());
        return Json(new ObjectResponseModel<Room> { Data = room });
    }
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var room = await Task.FromResult(_roomsService.GetRoom(id));
        if (room == null) return NoContent();
        return Json(new ObjectResponseModel<Room> { Data = room });
    }
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var room = await Task.FromResult(_roomsService.GetRoom(id));
        if (room == null) return NoContent();
        _roomsService.DeleteRoom(id);
        return Json(new ObjectResponseModel<Room> { Data = room });
    }
}