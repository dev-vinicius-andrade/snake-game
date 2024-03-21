namespace Library.Commons.Eventbus.RabbitMq.Abstractions
{
    public abstract class BaseRabbitMqConfigurations
    {
        protected BaseRabbitMqConfigurations( Dictionary<string, object>? args =null)
        {
            Arguments = args?? new Dictionary<string, object>();
        }
        public string Name { get; set; } = null!;
        public bool Durable { get; set; }
        public bool Exclusive { get; set; }
        public bool AutoDelete { get; set; }
        public Dictionary<string, object> Arguments { get; set; }

    }
}
