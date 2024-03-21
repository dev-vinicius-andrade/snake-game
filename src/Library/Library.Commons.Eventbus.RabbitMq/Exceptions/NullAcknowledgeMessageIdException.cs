namespace Library.Commons.Eventbus.RabbitMq.Exceptions
{
    public class NullAcknowledgeMessageIdException : Exception
    {

        public NullAcknowledgeMessageIdException(string?message=null,Exception? exception=null) : base( 
                string.IsNullOrEmpty(message) ? "An error occurred while acknowledging the message " : message, exception)
        { }
    }
}
