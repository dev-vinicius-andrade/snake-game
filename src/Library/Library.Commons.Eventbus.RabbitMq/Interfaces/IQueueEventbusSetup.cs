namespace Library.Commons.Eventbus.RabbitMq.Interfaces
{
    public interface IQueueEventbusSetup:IEventbusSetup
    {
        void CreateQueue(string name, bool durable = true, bool exclusive = false, bool autoDelete = false,
            IDictionary<string, object> arguments = null);
    }
}