using Application.Manager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Application.Manager.Api.Controllers;

[Route("[controller]")]
[ApiController]
[AllowAnonymous]
public class ServersController : Controller
{
    private readonly ContainerService _containersService;

    public ServersController(ContainerService containersService)
    {
        _containersService = containersService;
    }

    // GET
    [HttpGet]
    public IActionResult Index()
    {
        var a = _containersService.GetAvailableServers().Result;
        return Ok();
    }
}