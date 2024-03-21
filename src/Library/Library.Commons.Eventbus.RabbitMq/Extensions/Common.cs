namespace Library.Commons.Eventbus.RabbitMq.Extensions
{
    internal static class Common
    {

        public static bool IsNullOrEmpty(this string? text)
        {
            return string.IsNullOrEmpty(text)|| string.IsNullOrWhiteSpace(text);
        }
    }
}
