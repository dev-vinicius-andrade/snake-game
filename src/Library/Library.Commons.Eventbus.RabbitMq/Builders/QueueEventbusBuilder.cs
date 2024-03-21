using Library.Commons.Eventbus.RabbitMq.Configurations;
using Library.Commons.Eventbus.RabbitMq.Extensions;
using Library.Commons.Eventbus.RabbitMq.Interfaces;
using RabbitMQ.Client;

namespace Library.Commons.Eventbus.RabbitMq.Builders
{
    public static class QueueEventbusBuilder
    {
        public static RabbitMqReaderConfiguration BuildDefaultConfiguration(string queueName,string? routingKey=null, bool configurePassive = false) => new RabbitMqReaderConfiguration
        {
            AutoDelete = false,
            Name = queueName,
            Exclusive = false,
            Durable = true,
            RoutingKey = routingKey??queueName
        };

        internal  static IMessageResponse<TEntity>? BuildRabbitMessageResponse<TEntity>(IModel? model, BasicGetResult? result, bool autoAck=false)
            where TEntity : class
        {

            if (result == null) return BuildNullMessageResponse<TEntity>();
            return new RabbitMqResponse<TEntity>(model, result.DeliveryTag.ToString(), result.Body,
                result.Body.ToArray().ToEntity<TEntity>(), "Message Read", autoAck);
        }

        internal static IMessageResponse<TEntity>? BuildNullMessageResponse<TEntity>()
            where TEntity : class
        {
            return null;
        }
    }
}
