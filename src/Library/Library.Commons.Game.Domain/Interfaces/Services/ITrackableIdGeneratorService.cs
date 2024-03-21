using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Library.Commons.Game.Domain.Interfaces.Services;

public interface ITrackableIdGeneratorService
{
    ITrackableId Generate(Guid? guid=null);
    bool IsType<T>(ITrackableId trackableId);

}