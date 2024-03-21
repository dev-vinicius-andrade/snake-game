using Library.Commons.Eventbus.RabbitMq.Exceptions;
using Library.Commons.Eventbus.RabbitMq.Interfaces;
using RabbitMQ.Client;

namespace Library.Commons.Eventbus.RabbitMq
{
    public class RabbitMqResponse<TEntity>:IMessageResponse<TEntity>
    where TEntity:class
    {
        public Exception? Exception { get; }
        private readonly IModel? _model;
        private readonly bool _autoAck;
        private readonly bool _hasError;
        public string Message { get; }
        public string? MessageId { get; internal set; }
        public TEntity? Value { get; internal set; }
        internal RabbitMqResponse(IModel? model, string messageId, ReadOnlyMemory<byte> body, TEntity value,
            string message, bool autoAck = false)
        {
            _model = model;
            _autoAck = autoAck;
            MessageId = messageId;
            Value = value;
            Message = message;
            _hasError = false;
            RawMessage = body;
        }

        internal RabbitMqResponse(Exception exception)
        {
            Exception = exception;
            _hasError = true;
            Message = exception.Message;
        }

        public void Acknowledge()
        {
            if (_model == null) throw new NullReferenceException("Model cannot be null");
            if(_autoAck) return;
            _model.BasicAck(ConvertMessageId(), false);

        }

        public void Discard(bool requeue = false)
        {
            if (_model == null) throw new NullReferenceException("Model cannot be null");
            _model.BasicNack(ConvertMessageId(), false, requeue);
        }

        public bool HasError() => _hasError;
        public ReadOnlyMemory<byte> RawMessage { get; }


        private ulong ConvertMessageId()
        {
            if (string.IsNullOrWhiteSpace(MessageId))
                throw new NullAcknowledgeMessageIdException();

            if (!ulong.TryParse(MessageId, out var convertedMessageId))
                throw new AcknowledgeMessageIdConversionException(MessageId);
            return convertedMessageId;
        }
        public bool HasValue()
        {
            return Value != null;
        }
    }
}
