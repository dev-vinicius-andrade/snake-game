using System.ComponentModel;

namespace Library.Commons.Game.Domain.Enums;

public enum Direction
{
    [Description("Left")] Left = 0,
    [Description("Up")] Up = 1,
    [Description("Right")] Right = 2,
    [Description("Down")] Down = 3,
    [Description("Angular")] Angular = 4
}