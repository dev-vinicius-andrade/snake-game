using System.Text.Json.Serialization;
using Library.Commons.Eventbus.RabbitMq.Abstractions;
using Library.Commons.Eventbus.RabbitMq.Enums;

namespace Library.Commons.Eventbus.RabbitMq.Configurations
{
    public class RabbitMqPublisherConfiguration:BaseRabbitMqConfigurations
    {
        public RabbitMqPublisherConfiguration():base()
        {
            QueuesConfiguration=new List<RabbitMqReaderConfiguration>();
            CreateDefaultQueue = false;
        }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ExchangeTypeEnum Type { get; set; }

        public bool CreateDefaultQueue { get; set; }
        public List<RabbitMqReaderConfiguration> QueuesConfiguration { get; set; }
    }
}
