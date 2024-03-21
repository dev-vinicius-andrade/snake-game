using Library.Commons.Eventbus.RabbitMq.Abstractions;

namespace Library.Commons.Eventbus.RabbitMq.Configurations
{
    public class RabbitMqReaderConfiguration:BaseRabbitMqConfigurations
    {
        public RabbitMqReaderConfiguration()
        {
            RoutingKey = null;
        }
        public string? RoutingKey { get; set; }
    }
}
