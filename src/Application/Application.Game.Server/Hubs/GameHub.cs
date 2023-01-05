using Domain.Game.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Application.Game.Server.Hubs
{
    public class GameHub:Hub<IGameHub>
    {
    }
}
