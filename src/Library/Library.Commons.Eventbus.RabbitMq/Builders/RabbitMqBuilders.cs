using Library.Commons.Eventbus.RabbitMq.Abstractions;
using Library.Commons.Eventbus.RabbitMq.Configurations;
using Library.Commons.Eventbus.RabbitMq.Interfaces;
using Library.Commons.Eventbus.RabbitMq.Setup;
using RabbitMQ.Client;

namespace Library.Commons.Eventbus.RabbitMq.Builders
{
    public class RabbitMqBuilders : BaseEventBus
    {
        public RabbitMqBuilders(Uri uri) : base(uri) { }
        public RabbitMqBuilders(string hostName) : base(hostName) { }
        public RabbitMqBuilders(IConnection connection) : base(connection) { }
        public RabbitMqBuilders(IConnectionFactory connectionFactory) : base(connectionFactory.CreateConnection()) { }
        public RabbitMqBuilders(Action<ConnectionFactory> connectionFactory) : base(connectionFactory) { }

        public IExchangeEventbusSetup BuidlEventbusSetup(RabbitMqPublisherConfiguration configuration, IConnection connection = null)
        => new ExchangeEventbusSetup(connection ?? Connection, configuration);

        public IQueueBinder BuildQueueBinder(IConnection connection = null) => new QueueBinderSetup(connection);

        public IEventbusConsumerSetup<TEntity> BuildConsumer<TEntity>(IConnection connection = null)
            where TEntity : class => new EventbusConsumerSetup<TEntity>(connection ?? Connection);
        
        public IQueueEventbusSetup BuidlEventbusSetup(IReadOnlyList<RabbitMqReaderConfiguration> configuration, IConnection connection = null)
        => new QueueEventbusSetup(connection ?? Connection, configuration);

        public IEventbusReader BuildReader(IConnection connection = null)
        => new EventbusReader(connection ?? Connection);

        public IEventbusPublisher<IBasicProperties> BuildPublisher(IConnection connection = null)
        {
            return new EventbusPublisher(connection ?? Connection);
        }
        public static IConnection BuildConnection(Action<ConnectionFactory> factory)
        {
            var connectionFactory = new ConnectionFactory();
            factory.Invoke(connectionFactory);
            return connectionFactory.CreateConnection();
        }

    }
}
