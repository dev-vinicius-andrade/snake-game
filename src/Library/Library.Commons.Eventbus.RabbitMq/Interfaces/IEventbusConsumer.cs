using Library.Commons.Eventbus.RabbitMq.EventArgs;

namespace Library.Commons.Eventbus.RabbitMq.Interfaces
{
    public interface IEventbusConsumer<TEntity>:IDisposable where TEntity:class
    {
        event EventHandler<EventbusConsumerMessageReceivedEventArgs<TEntity>> OnMessageReceived;
        void Start();
        void Stop();
    }
}