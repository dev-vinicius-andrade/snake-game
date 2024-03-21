using System.ComponentModel;

namespace Library.Commons.Eventbus.RabbitMq.Extensions
{
    internal  static class Enums
    {
        public static string Description<TEntity>(this TEntity val) where TEntity : Enum
        {
           var attributes = (DescriptionAttribute[])val
                .GetType()
                .GetField(val.ToString())
                .GetCustomAttributes(typeof(DescriptionAttribute), false);
            return attributes.Length > 0 ? attributes[0].Description : null;
        }
    }
}
