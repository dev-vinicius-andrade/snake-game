using System.Text;
using System.Text.Json;

namespace Library.Commons.Eventbus.RabbitMq.Extensions
{
    internal static class Converters
    {
        public static  byte[] ToByteArray<TEntity>(this TEntity entity) where TEntity : class
        {
            try
            {
                var jsonString = JsonSerializer.Serialize(entity);
                return Encoding.UTF8.GetBytes(jsonString);
            }
            catch (Exception)
            {
                return null;
            }
        }
        public static TEntity ToEntity<TEntity>(this byte[] byteArray) where TEntity : class
        {
            try
            {
                var jsonString = Encoding.UTF8.GetString(byteArray);
                if (typeof(TEntity) == typeof(string))
                    return jsonString as TEntity;

                return JsonSerializer.Deserialize<TEntity>(jsonString);
            }
            catch (Exception)
            {
                return null;
            }
        }

    }
}
