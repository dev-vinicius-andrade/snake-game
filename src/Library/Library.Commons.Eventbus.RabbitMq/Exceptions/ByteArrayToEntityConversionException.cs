namespace Library.Commons.Eventbus.RabbitMq.Exceptions
{
    public class ByteArrayToEntityConversionException<TEntity> : Exception
    {

        public ByteArrayToEntityConversionException(Exception ex = null) : base(
            $"Unable to byte array to {typeof(TEntity)}", ex)
        { }
    }
}
