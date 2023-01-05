using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Application.Game.Server.Controllers;
[Route("[controller]")]
[ApiController]
[AllowAnonymous]
public class AvailableController : Controller
{
    // GET
    public IActionResult Index()
    {
        return View();
    }
}