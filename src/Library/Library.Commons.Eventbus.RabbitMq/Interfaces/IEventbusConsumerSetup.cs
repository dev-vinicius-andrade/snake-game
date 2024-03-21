namespace Library.Commons.Eventbus.RabbitMq.Interfaces
{
    public interface IEventbusConsumerSetup<TEntity> where  TEntity:class
    {
        IEventbusConsumer<TEntity> Configure(string queueName, bool autoDelete = true, bool durable = false,
            bool exclusive = false, IDictionary<string, object>? arguments = null, bool autoAck = false,
             bool declareQueue = true
           );
    }
}