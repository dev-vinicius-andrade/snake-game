using Library.Commons.Eventbus.RabbitMq.Abstractions;
using Library.Commons.Eventbus.RabbitMq.Extensions;
using RabbitMQ.Client;

namespace Library.Commons.Eventbus.RabbitMq
{
    internal class EventbusPublisher : BaseEventbusPublisher<IBasicProperties>
    {
        public EventbusPublisher(Uri uri) : base(uri)
        {
        }

        public EventbusPublisher(string hostName) : base(hostName)
        {
        }

        public EventbusPublisher(IConnection connection) : base(connection)
        {
        }

        protected override RabbitPublishResult Publish<TEntity>(IModel model, TEntity entity, string exchangeName, string routingKey,
            bool persitentMessage = true, IBasicProperties? publishConfiguration = default)
        {
            try
            {
                if(exchangeName.IsNullOrEmpty() && routingKey.IsNullOrEmpty())
                    throw new  ArgumentNullException($"{nameof(exchangeName)}, {nameof(routingKey)}", "At least exchangeName or routingKey must be provided to publish message.");
                var properties = publishConfiguration ?? model.CreateBasicProperties();
                if (publishConfiguration == null)
                    properties.Persistent = persitentMessage;

                model.BasicPublish(
                    exchange: exchangeName.IsNullOrEmpty() ? string.Empty : exchangeName,
                    routingKey: routingKey.IsNullOrEmpty() ? string.Empty : routingKey,
                    basicProperties: properties,
                    body: entity.ToByteArray());
                return new  RabbitPublishResult($"Message published{ReturnMessageComplement(exchangeName,routingKey)}" +
                                           $" {(routingKey.IsNullOrEmpty() ? string.Empty : "RoutingKey:" + routingKey)}");

            }
            catch (Exception ex)
            {
                return new RabbitPublishResult($"Error publishing message{ReturnMessageComplement(exchangeName,routingKey)} " , ex);
            }
        }

        private string ReturnMessageComplement(string exchangeName, string routingKey)
        {
            
            if (!exchangeName.IsNullOrEmpty() || !routingKey.IsNullOrEmpty())
                return
                    $" to {(exchangeName.IsNullOrEmpty() ? string.Empty : "ExchangeName:" + exchangeName)} {(routingKey.IsNullOrEmpty() ? string.Empty : "RoutingKey:" + routingKey)}.";

            return ".";
        }

    }
}
