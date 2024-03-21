using Library.Commons.Eventbus.RabbitMq.Interfaces;
using RabbitMQ.Client;

namespace Library.Commons.Eventbus.RabbitMq.Abstractions
{
    public abstract class BaseEventbusPublisher<TMessagePublishConfiguration> : BaseEventBus,  IEventbusPublisher<TMessagePublishConfiguration>
        where TMessagePublishConfiguration : class

    {
        protected BaseEventbusPublisher(Uri uri) : base(uri){}
        protected BaseEventbusPublisher(string hostName) : base(hostName) { }
        protected BaseEventbusPublisher(IConnection connection) : base(connection) { }
        
        protected abstract RabbitPublishResult Publish<TEntity>(IModel model, TEntity entity, string exchangeName, string routingKey, bool persitentMessage = true,
            TMessagePublishConfiguration? publishConfiguration = default) where TEntity : class;



        public IPublishResult Publish<TEntity>(TEntity entity, string exchangeName, string routingKey,
            bool persitentMessage = true,
            TMessagePublishConfiguration? publishConfiguration = default) where TEntity : class
        {
            var model = Connection.CreateModel();
            var result = Publish(model, entity, exchangeName, routingKey, persitentMessage, publishConfiguration);
            model.Close();
            return result;
        }

    }
}
