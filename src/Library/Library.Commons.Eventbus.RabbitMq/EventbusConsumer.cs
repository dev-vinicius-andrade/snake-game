using Library.Commons.Eventbus.RabbitMq.Builders;
using Library.Commons.Eventbus.RabbitMq.EventArgs;
using Library.Commons.Eventbus.RabbitMq.Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Library.Commons.Eventbus.RabbitMq
{
    internal class EventbusConsumer<TEntity> : IEventbusConsumer<TEntity>
    where TEntity:class
    {
        private readonly bool _autoAck;
        private readonly IModel _model;
        private readonly string _queueName;
        public event EventHandler<EventbusConsumerMessageReceivedEventArgs<TEntity>>? OnMessageReceived;
        public EventbusConsumer(IModel model, string queueName, bool autoAck=false)
        {
            _model = model;
            _queueName = queueName;
            _autoAck = autoAck;
            
        }


        public void Start()
        {
            var consumer = new EventingBasicConsumer(_model);
            
            
            consumer.Received += (sender, response) =>
            {
                var result = QueueEventbusBuilder.BuildRabbitMessageResponse<TEntity>(
                    model: consumer.Model,
                    result: new BasicGetResult(

                        deliveryTag: response.DeliveryTag,
                        redelivered: response.Redelivered,
                        exchange: response.Exchange,
                        routingKey: response.RoutingKey,
                        messageCount: 1,
                        basicProperties: _model.CreateBasicProperties(),
                        body: response.Body
                    )
                    , autoAck: _autoAck
                );
                OnMessageReceived?.Invoke(this, new EventbusConsumerMessageReceivedEventArgs<TEntity>(result, this));

            };
            _model.BasicConsume(_queueName, _autoAck, consumer);
        }

        public void Stop()=> Dispose();
        public void Dispose()
        {
            _model?.Dispose();
            
        }
    }
}
