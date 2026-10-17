using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;

namespace Application.Game.Server.Workers;

/// <summary>Tells the manager every few seconds which rooms this server has and how many players are in them.</summary>
public class RoomHeartbeatWorkerService(
    IRoomsService rooms, IPlayerRoomService playerRooms, IManagementApiService management, Serilog.ILogger logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            await SendAsync();
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SendAsync()
    {
        try
        {
            var snapshots = rooms.GetRooms().Select(room => new RoomSnapshot(room, (int)playerRooms.CountPlayers(room))).ToArray();
            await management.SendHeartbeatAsync(snapshots);
        }
        catch (Exception exception)
        {
            logger.Warning(exception, "Could not send the room heartbeat");
        }
    }
}
