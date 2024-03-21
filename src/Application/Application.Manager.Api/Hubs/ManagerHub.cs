using Microsoft.AspNetCore.SignalR;

namespace Application.Manager.Api.Hubs;

public class ManagerHub:Hub
{
    //public async Task PlayerJoined(Guid roomId, string nickname)
    //{
    //    await Clients.roomId.ToString()).SendAsync("PlayerJoined", nickname);
    //}
  
    public override async Task OnConnectedAsync()
    {
        await Task.CompletedTask;
    }

  
}