namespace Library.Commons.Eventbus.RabbitMq.Interfaces
{
    public interface IMessageResponse<out TEntity> where TEntity:class
    {
        public string MessageId { get; }
        public TEntity Value { get; }
        void Acknowledge();
        void Discard(bool requeue=false);

        bool HasError();
        ReadOnlyMemory<byte> RawMessage { get; }
        string Message { get; }
    }
}