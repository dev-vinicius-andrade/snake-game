using Library.Commons.Eventbus.RabbitMq.Abstractions;
using Library.Commons.Eventbus.RabbitMq.Builders;
using Library.Commons.Eventbus.RabbitMq.Exceptions;
using Library.Commons.Eventbus.RabbitMq.Extensions;
using Library.Commons.Eventbus.RabbitMq.Interfaces;
using RabbitMQ.Client;

namespace Library.Commons.Eventbus.RabbitMq
{
    internal sealed class EventbusReader : BaseEventbusReader
    {

        public EventbusReader(Uri uri) : base(uri)
        { }

        public EventbusReader(string hostName) : base(hostName)
        { }
        public EventbusReader(IConnection connection) : base(connection)
        { }

        protected override long Count(IModel model, string queueName)
        {
            try
            {
                return queueName.IsNullOrEmpty() ? throw new InvalidQueueNameException(queueName) : model.QueueDeclarePassive(queueName).MessageCount;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        protected override IMessageResponse<TEntity>? Read<TEntity>(IModel? model, string queueName) where TEntity : class
        {
            try
            {
                if (queueName.IsNullOrEmpty())
                    throw new InvalidQueueNameException();

                var basicGetResult = model?.BasicGet(queueName, false);
                return QueueEventbusBuilder.BuildRabbitMessageResponse<TEntity>(model, basicGetResult);
            }
            catch (Exception ex)
            {
                return  new RabbitMqResponse<TEntity>(ex);
            }
        }



    }
}
