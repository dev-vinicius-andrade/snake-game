using Library.Commons.Eventbus.RabbitMq.Abstractions;
using Library.Commons.Eventbus.RabbitMq.Constants;
using Library.Commons.Eventbus.RabbitMq.Extensions;
using Library.Commons.Eventbus.RabbitMq.Interfaces;
using RabbitMQ.Client;

namespace Library.Commons.Eventbus.RabbitMq.Setup
{
    internal class EventbusConsumerSetup<TEntity> : BaseEventBus, IEventbusConsumerSetup<TEntity>
    where TEntity:class
    {
        public EventbusConsumerSetup(Uri uri, int retryAttempts = DefaultValues.DefaultRetryAttempts, int delay = DefaultValues.DefaultRetryDelay) : base(uri,retryAttempts,delay)
        {
        }

        public EventbusConsumerSetup(string hostName, int retryAttempts = DefaultValues.DefaultRetryAttempts, int delay = DefaultValues.DefaultRetryDelay) : base(hostName,retryAttempts,delay)
        {
        }

        public EventbusConsumerSetup(IConnection connection) : base(connection)
        {
        }

        public EventbusConsumerSetup(IConnectionFactory factory, int retryAttempts = DefaultValues.DefaultRetryAttempts, int delay = DefaultValues.DefaultRetryDelay) : base(factory, retryAttempts, delay)
        {
        }

        public EventbusConsumerSetup(Action<ConnectionFactory> factory) : base(factory)
        {
        }
        
        public IEventbusConsumer<TEntity> Configure(string queueName,   bool autoDelete = true, bool durable = false, bool exclusive = false, IDictionary<string, object>? arguments = null, bool autoAck = false, bool declareQueue=true)
        {
            if (queueName.IsNullOrEmpty()) throw new ArgumentNullException(queueName);
            var model = Connection.CreateModel();
            if (declareQueue)
                model.QueueDeclare(queueName, durable, exclusive, autoDelete, arguments);
            return new EventbusConsumer<TEntity>(model, queueName, autoAck);
        }
    }
}
