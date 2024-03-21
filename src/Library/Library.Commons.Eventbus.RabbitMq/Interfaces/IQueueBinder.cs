namespace Library.Commons.Eventbus.RabbitMq.Interfaces
{
    public interface IQueueBinder
    {
        void BindQueue(string queue, string exchange,string routingKey, IDictionary<string,object>arguments=null);
    }
}