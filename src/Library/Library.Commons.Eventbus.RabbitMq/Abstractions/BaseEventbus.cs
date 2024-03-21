using Library.Commons.Eventbus.RabbitMq.Constants;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Library.Commons.Eventbus.RabbitMq.Abstractions
{
    public abstract class BaseEventBus : IDisposable
    {
        public readonly IConnection Connection;
        protected BaseEventBus(Uri uri, int retryAttempts = DefaultValues.DefaultRetryAttempts, int delay = DefaultValues.DefaultRetryDelay)
            : this(new ConnectionFactory { Uri = uri },retryAttempts,delay) { }
        protected BaseEventBus(string hostName, int retryAttempts = DefaultValues.DefaultRetryAttempts, int delay = DefaultValues.DefaultRetryDelay) 
            : 
            this(new ConnectionFactory { HostName = hostName, AutomaticRecoveryEnabled = true },retryAttempts,delay) { }

        protected BaseEventBus(IConnection connection)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        protected BaseEventBus(IConnectionFactory factory, int retryAttempts = DefaultValues.DefaultRetryAttempts, int delay = DefaultValues.DefaultRetryDelay)
        {
            Connection = CreateConnection(factory,retryAttempts,delay);
        }

        private IConnection CreateConnection(IConnectionFactory factory, int retryAttempts = DefaultValues.DefaultRetryAttempts, int delay = DefaultValues.DefaultRetryDelay)
        {
            var attempts = 0;
            while (attempts < retryAttempts)
            {
                try
                {
                    return factory.CreateConnection();
                }
                catch (BrokerUnreachableException)
                {
                    attempts++;
                    Thread.Sleep(delay);
                }
            }
            throw new BrokerUnreachableException(new Exception($"Unable to connect to {factory.Uri.ToString()}"));
        }

        protected BaseEventBus(Action<ConnectionFactory> factory)
        {
            var connectionFactory = new ConnectionFactory();
            factory.Invoke(connectionFactory);
            Connection = connectionFactory.CreateConnection();
        }
        public virtual void Dispose()
        {
            Connection?.Dispose();
        }
    }
}
