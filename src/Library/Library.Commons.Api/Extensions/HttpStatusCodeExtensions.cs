using System.Net;

namespace Library.Commons.Api.Extensions;

public static class HttpStatusCodeExtensions
{
    public static int ToInt(this HttpStatusCode statusCode, HttpStatusCode defaultValue = HttpStatusCode.InternalServerError) => Convert.ToInt32(statusCode);
}