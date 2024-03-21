using Library.Commons.Eventbus.RabbitMq.Abstractions;
using Library.Commons.Eventbus.RabbitMq.Configurations;
using Library.Commons.Eventbus.RabbitMq.Constants;
using Library.Commons.Eventbus.RabbitMq.Extensions;
using Library.Commons.Eventbus.RabbitMq.Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Library.Commons.Eventbus.RabbitMq.Setup
{
    public sealed class ExchangeEventbusSetup : BaseEventBus, IExchangeEventbusSetup
    {
        private readonly RabbitMqPublisherConfiguration _configuration;
        private readonly HashSet<string> _bindedQueues;
        public ExchangeEventbusSetup(Uri uri, RabbitMqPublisherConfiguration configuration, int retryAttempts = DefaultValues.DefaultRetryAttempts, int delay = DefaultValues.DefaultRetryDelay) : base(uri,retryAttempts,delay)
        {
            _configuration = configuration;
            _bindedQueues = new HashSet<string>();
        }

        public ExchangeEventbusSetup(string hostName, RabbitMqPublisherConfiguration configuration, int retryAttempts = DefaultValues.DefaultRetryAttempts, int delay = DefaultValues.DefaultRetryDelay) : base(hostName)
        {
            _configuration = configuration;
            _bindedQueues = new HashSet<string>();
        }
        public ExchangeEventbusSetup(IConnection connection, RabbitMqPublisherConfiguration configuration) : base(connection)
        {
            _configuration = configuration;
            _bindedQueues = new HashSet<string>();
        }

        public void Configure()
        {

                var model = Connection.CreateModel();
                ConfigureExchange(model);

                if ((!_configuration.QueuesConfiguration.Any()) && _configuration.CreateDefaultQueue)
                    CreateAndBindQueue(CreateExchangeDefaultQueueConfiguration());
                else if(_configuration.QueuesConfiguration.Any())
                    foreach (var queueConfiguration in _configuration.QueuesConfiguration)
                        CreateAndBindQueue(queueConfiguration);
                model.Close();

        }
        private void ConfigureExchange(IModel model)
        {
            try
            {
                model.ExchangeDeclarePassive(_configuration.Name);
            }
            catch (OperationInterruptedException ex)
            {
                if (ex.ShutdownReason.ReplyCode != 404)
                    throw;
                ExchangeDeclare();
            }
        }

        private void ExchangeDeclare()
        {
            var model = Connection.CreateModel();
            model.ExchangeDeclare(
                exchange: _configuration.Name,
                durable: _configuration.Durable,
                autoDelete: _configuration.AutoDelete,
                arguments: _configuration.Arguments,
                type: _configuration.Type.Description());
            model.Close();

        }

        private RabbitMqReaderConfiguration CreateExchangeDefaultQueueConfiguration()
        {
            return new RabbitMqReaderConfiguration
            {
                Name = $"{_configuration.Name}{DefaultValues.DefaultQueueNameSuffix}",
                Durable = _configuration.Durable,
                AutoDelete = _configuration.AutoDelete,
                Exclusive = _configuration.Exclusive,
                Arguments = _configuration.Arguments,
                RoutingKey = $"{_configuration.Name}{DefaultValues.DefaultQueueNameSuffix}"

            };
        }

        private void CreateAndBindQueue(RabbitMqReaderConfiguration readerConfiguration)
        {
            if (_bindedQueues.Contains(readerConfiguration.Name))
                return;
            var queueSetup = new QueueEventbusSetup(Connection, new List<RabbitMqReaderConfiguration>{readerConfiguration});
            var queueBinder = new QueueBinderSetup(Connection);

            queueSetup.Configure();

            queueBinder.BindQueue(queue: readerConfiguration.Name,
                exchange: _configuration.Name,
                routingKey: readerConfiguration.RoutingKey??string.Empty,
                arguments: _configuration.Arguments);

            _bindedQueues.Add(readerConfiguration.Name);
        }
    }
}
