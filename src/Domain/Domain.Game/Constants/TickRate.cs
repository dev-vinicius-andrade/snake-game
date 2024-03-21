using Library.Commons.Game.Domain.Entities.Configurations;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Constants;

public static class TickRate
{
    public static ITimeSpanConfiguration DefaultPlayerMovementTickRate { get; } = new TimeSpanConfiguration
    {
        Milliseconds = 1000
    };
    
}