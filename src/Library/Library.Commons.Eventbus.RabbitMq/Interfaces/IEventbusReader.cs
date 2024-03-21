namespace Library.Commons.Eventbus.RabbitMq.Interfaces
{
    public interface IEventbusReader:IDisposable
    {
        long Count(string queueName);
        IMessageResponse<TEntity>? Read<TEntity>(string queueName)where TEntity : class;
        IReadOnlyList<IMessageResponse<TEntity>?> Read<TEntity>(string queueName, long limit=1) where TEntity : class;
    }
}