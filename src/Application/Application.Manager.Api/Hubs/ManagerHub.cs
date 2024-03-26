using Microsoft.AspNetCore.SignalR;

namespace Application.Manager.Api.Hubs;

public class ManagerHub:Hub
{
    public override async Task OnConnectedAsync()
    {
        await Task.CompletedTask;
    }
}