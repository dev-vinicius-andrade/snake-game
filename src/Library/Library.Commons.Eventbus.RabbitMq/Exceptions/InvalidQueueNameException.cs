namespace Library.Commons.Eventbus.RabbitMq.Exceptions
{
    public class InvalidQueueNameException:Exception
    {

        public InvalidQueueNameException(string message = null) : base(
            $"You must provide a valid queueName. {(string.IsNullOrEmpty(message) ? string.Empty : "Aditional Message:" + message)}")
        { }
    }
}
