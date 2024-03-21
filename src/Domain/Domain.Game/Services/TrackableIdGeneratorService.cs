using Domain.Game.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;

namespace Domain.Game.Services;

internal class TrackableIdGeneratorService : ITrackableIdGeneratorService

{
    public ITrackableId Generate(Guid? guid = null)
    {
        return new TrackableId(guid ?? Guid.NewGuid());
    }
    public  bool IsType<T>(ITrackableId trackableId)
    {
        var result =  trackableId is T;
        return result;
        
    }
}