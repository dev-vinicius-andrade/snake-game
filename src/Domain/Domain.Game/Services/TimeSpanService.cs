using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;

namespace Domain.Game.Services;

internal class TimeSpanService : ITimeSpanService
{
    public TimeSpan ToTimeSpan<T>(T? timeSpanConfiguration=default, double defaultValue = 0) where T : ITimeSpanConfiguration
    {
        return timeSpanConfiguration == null
            ? TimeSpan.Zero
            : TimeSpan.Zero
                .Add(TimeSpan.FromDays(timeSpanConfiguration.Days ?? defaultValue))
                .Add(TimeSpan.FromHours(timeSpanConfiguration.Hours ?? defaultValue))
                .Add(TimeSpan.FromMinutes(timeSpanConfiguration.Minutes ?? defaultValue))
                .Add(TimeSpan.FromSeconds(timeSpanConfiguration.Seconds ?? defaultValue))
                .Add(TimeSpan.FromMilliseconds(timeSpanConfiguration.Milliseconds ?? defaultValue));
    }
}