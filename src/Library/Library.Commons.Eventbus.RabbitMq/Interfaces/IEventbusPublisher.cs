namespace Library.Commons.Eventbus.RabbitMq.Interfaces
{
    public interface IEventbusPublisher<in TMessagePublishConfiguration>:
        IDisposable
        where TMessagePublishConfiguration : class
    {
        IPublishResult Publish<TEntity>(TEntity entity, string exchangeName, bool persitentMessage = true,  TMessagePublishConfiguration? publishConfiguration = default) where TEntity : class 
            => Publish(entity, exchangeName, string.Empty, persitentMessage,  publishConfiguration) ;



        IPublishResult Publish<TEntity>(TEntity entity, string exchangeName, string routingKey, bool persitentMessage=true, TMessagePublishConfiguration? publishConfiguration = default) where  TEntity:class;
    }
}
