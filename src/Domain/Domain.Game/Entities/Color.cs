using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Entities;

internal record Color(string FillColor, string Border) : IColor
{

}