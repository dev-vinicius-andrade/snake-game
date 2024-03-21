using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Entities;

internal record TrackableId(Guid Guid) :ITrackableId
{
    
}