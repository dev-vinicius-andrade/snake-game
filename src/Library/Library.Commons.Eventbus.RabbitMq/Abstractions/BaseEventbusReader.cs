using Library.Commons.Eventbus.RabbitMq.Interfaces;
using RabbitMQ.Client;

namespace Library.Commons.Eventbus.RabbitMq.Abstractions
{
    internal abstract class BaseEventbusReader : BaseEventBus, IEventbusReader
    {
        protected BaseEventbusReader(Uri uri) : base(uri) { }

        protected BaseEventbusReader(string hostName) : base(hostName) { }
        protected BaseEventbusReader(IConnection connection) : base(connection) { }
        public long Count(string queueName) => Count(Connection.CreateModel(), queueName);
        protected abstract long Count(IModel model, string queueName);

        public IMessageResponse<TEntity>? Read<TEntity>(string queueName) where TEntity : class
        {
            var model = Connection.CreateModel();
            var response=Read<TEntity>(model, queueName);
            return response;
        }


        public IReadOnlyList<IMessageResponse<TEntity>?> Read<TEntity>(string queueName, long limit = 1) where TEntity : class
        {
            var resultList = new List<IMessageResponse<TEntity>?>();
            try
            {
                while (resultList.Count < limit)
                {
                    var response = Read<TEntity>(queueName);

                    resultList.Add(response);
                }

                return resultList;
            }
            catch (Exception)
            {
                return new List<IMessageResponse<TEntity>?>();
            }
        }

        protected abstract IMessageResponse<TEntity>? Read<TEntity>(IModel? model, string queueName)
            where TEntity : class;

    }
}
