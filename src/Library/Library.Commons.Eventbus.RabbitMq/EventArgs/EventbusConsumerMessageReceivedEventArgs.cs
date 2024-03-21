using Library.Commons.Eventbus.RabbitMq.Interfaces;

namespace Library.Commons.Eventbus.RabbitMq.EventArgs
{
    public class EventbusConsumerMessageReceivedEventArgs<TEntity>:System.EventArgs
    where TEntity:class
    {
        public IMessageResponse<TEntity> Response { get; }
        public IDisposable EventbusChannel { get; }

        public EventbusConsumerMessageReceivedEventArgs(IMessageResponse<TEntity> response, IDisposable channel)
        {
            Response = response;
            EventbusChannel = channel;
        }

    }
}
