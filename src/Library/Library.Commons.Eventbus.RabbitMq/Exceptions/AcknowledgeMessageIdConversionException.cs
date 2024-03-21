namespace Library.Commons.Eventbus.RabbitMq.Exceptions
{
    public class AcknowledgeMessageIdConversionException : Exception
    {

        public AcknowledgeMessageIdConversionException(string messageId) : base(
            $"Unable to convert messageId argument:{messageId} to ulong.")
        { }
    }
}
