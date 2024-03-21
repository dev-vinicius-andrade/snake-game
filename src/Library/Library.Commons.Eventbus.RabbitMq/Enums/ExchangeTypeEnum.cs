using System.ComponentModel;

namespace Library.Commons.Eventbus.RabbitMq.Enums
{
    public enum ExchangeTypeEnum
    {
        [Description("fanout")] Fanout = 0,
        [Description("headers")] Headers = 1,
        [Description("topic")] Topic = 2,
        [Description("direct")] Direct = 3
    }
}