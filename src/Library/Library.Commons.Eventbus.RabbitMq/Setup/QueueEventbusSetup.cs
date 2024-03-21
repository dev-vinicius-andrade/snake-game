using Library.Commons.Eventbus.RabbitMq.Abstractions;
using Library.Commons.Eventbus.RabbitMq.Configurations;
using Library.Commons.Eventbus.RabbitMq.Constants;
using Library.Commons.Eventbus.RabbitMq.Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Library.Commons.Eventbus.RabbitMq.Setup
{
    public sealed class QueueEventbusSetup : BaseEventBus, IQueueEventbusSetup
    {
        private readonly IReadOnlyList<RabbitMqReaderConfiguration> _configurations;

        public QueueEventbusSetup(Uri uri, IReadOnlyList<RabbitMqReaderConfiguration> configuration, int retryAttempts = DefaultValues.DefaultRetryAttempts, int delay = DefaultValues.DefaultRetryDelay) : base(uri,retryAttempts,delay)
        {
            _configurations = configuration;
        }
        public QueueEventbusSetup(string hostName, IReadOnlyList<RabbitMqReaderConfiguration> configuration, int retryAttempts = DefaultValues.DefaultRetryAttempts, int delay = DefaultValues.DefaultRetryDelay) : base(hostName, retryAttempts, delay)
        {
            _configurations = configuration;
        }
        public QueueEventbusSetup(IConnection connection, IReadOnlyList<RabbitMqReaderConfiguration> configuration) : base(connection)
        {
            _configurations = configuration;
        }


        public void Configure()
        {
            if (_configurations == null || !_configurations.Any()) throw new ArgumentNullException($"{nameof(_configurations)} can't be null");

            foreach (var queueConfiguration in _configurations)
                try
                {
                    var model = Connection.CreateModel();
                    model.QueueDeclarePassive(queueConfiguration.Name);
                    model.Close();
                }
                catch (OperationInterruptedException ex)
                {
                    if (ex.ShutdownReason.ReplyCode != 404)
                        throw;
                    CreateQueue(name: queueConfiguration.Name,
                        durable: queueConfiguration.Durable,
                        exclusive: queueConfiguration.Exclusive,
                        autoDelete: queueConfiguration.AutoDelete,
                        arguments: queueConfiguration.Arguments);
                }
        }

        public void CreateQueue(string name, bool durable = true, bool exclusive = false, bool autoDelete = false, IDictionary<string, object> arguments = null)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException($"{nameof(name)} can't be null");

            var model = Connection.CreateModel();
            model.QueueDeclare(queue: name,
                durable: durable,
                exclusive: exclusive,
                autoDelete: autoDelete,
                arguments: arguments);
            model.Close();

        }
    }
}
