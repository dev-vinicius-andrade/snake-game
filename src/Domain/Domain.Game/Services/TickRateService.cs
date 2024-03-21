using Library.Commons.Game.Domain.Interfaces.Services;

namespace Domain.Game.Services;
internal class TickRateService: ITickRateService
{

    public TimeSpan TickRate { get; }
    public TickRateService()
    {
        TickRate = TimeSpan.FromMilliseconds(1000/60.0);
    }

}