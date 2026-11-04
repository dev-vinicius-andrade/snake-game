namespace Application.Game.Server.Services;

/// <summary>
/// Starts a send to a player and lets go of it. The game loop must never wait for a player's link: a write to a slow one does not
/// finish until the link catches up, and a loop that waits there stops every other player in the room as well.
/// </summary>
public static class Detached
{
    public static void Run(Func<Task> send, Action<Exception> onFailure)
    {
        ArgumentNullException.ThrowIfNull(send);
        ArgumentNullException.ThrowIfNull(onFailure);
        _ = Task.Run(async () =>
        {
            try
            {
                await send();
            }
            catch (Exception failure)
            {
                onFailure(failure);
            }
        });
    }
}
