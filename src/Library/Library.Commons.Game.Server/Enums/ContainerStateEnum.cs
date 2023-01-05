using Ardalis.SmartEnum;
using Library.Commons.Game.Server.Constants;

namespace Library.Commons.Game.Server.Enums;

public sealed class ContainerStateEnum:SmartEnum<ContainerStateEnum,string>
{
    private ContainerStateEnum(string name, string value) : base(name, value)
    {
    }
    public static readonly ContainerStateEnum Running = new(nameof(Running), ContainerStateNames.Running);
}