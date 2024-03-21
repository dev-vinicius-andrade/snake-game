using Library.Commons.Eventbus.RabbitMq.Abstractions;
using Library.Commons.Eventbus.RabbitMq.Constants;
using Library.Commons.Eventbus.RabbitMq.Extensions;
using Library.Commons.Eventbus.RabbitMq.Interfaces;
using RabbitMQ.Client;

namespace Library.Commons.Eventbus.RabbitMq.Setup
{
    internal class QueueBinderSetup:BaseEventBus, IQueueBinder
    {
        public QueueBinderSetup(Uri uri, int retryAttempts = DefaultValues.DefaultRetryAttempts, int delay = DefaultValues.DefaultRetryDelay) : base(uri,retryAttempts,delay) { }
        public QueueBinderSetup(string hostName, int retryAttempts = DefaultValues.DefaultRetryAttempts, int delay = DefaultValues.DefaultRetryDelay) : base(hostName,retryAttempts,delay) { }
        public QueueBinderSetup(IConnection connection) : base(connection) { }
        public QueueBinderSetup(IConnectionFactory factory, int retryAttempts=DefaultValues.DefaultRetryAttempts, int delay=DefaultValues.DefaultRetryDelay) : base(factory, retryAttempts, delay) { }
        public QueueBinderSetup(Action<ConnectionFactory> factory) : base(factory) { }
        public void BindQueue(string queue, string exchange, string routingKey, IDictionary<string, object>? arguments = null)
        {
                var persistedRoutingKey = routingKey.IsNullOrEmpty() ? string.Empty : routingKey;
                Connection.CreateModel().QueueBind(queue,exchange, persistedRoutingKey, arguments);

        }
    }
}
