using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Library.Commons.Game.Domain.Interfaces.Services;

public interface ITimeSpanService
{
    TimeSpan ToTimeSpan<T>(T? timeSpanConfiguration = default, double defaultValue = 0) where T : ITimeSpanConfiguration;
}