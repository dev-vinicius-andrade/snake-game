using Library.Commons.Eventbus.RabbitMq.Interfaces;

namespace Library.Commons.Eventbus.RabbitMq
{
    public class RabbitPublishResult:IPublishResult
    {
        public string? Message { get; }
        private readonly bool _hasError;
        public Exception? Exception { get; }
        public RabbitPublishResult() { }
        public RabbitPublishResult(string message, bool hasError=false)
        {
            _hasError = hasError;
            Message = message;
        }
        public RabbitPublishResult(string message, Exception exception)
        {
            _hasError = true;
            Exception = exception;
            Message =$"{message}  ExceptionMessage:{exception.Message}";
            
        }

        public bool HasError() => _hasError;
    }
}
