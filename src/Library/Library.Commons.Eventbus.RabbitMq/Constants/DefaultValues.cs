namespace Library.Commons.Eventbus.RabbitMq.Constants
{
    internal class DefaultValues
    {
        internal const string DefaultQueueNameSuffix = "_DEFAULT_QUEUE";
        internal const string DefaultExchangeNameSuffix = "_DEFAULT_EXCHANGE";
        internal const int DefaultRetryAttempts = 10;
        internal const int DefaultRetryDelay = 2500;
    }
}
