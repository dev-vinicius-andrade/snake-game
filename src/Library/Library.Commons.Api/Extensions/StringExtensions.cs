namespace Library.Commons.Api.Extensions;

public static class StringExtensions
{
    public static bool IsNullOrEmpty(this string text)
    {
        return string.IsNullOrEmpty(text);
    }
    public static bool IsNullOrWhiteSpace(this string text)
    {
        return string.IsNullOrWhiteSpace(text);
    }
    public static bool IsNullOrEmptyOrWhiteSpace(this string text)
    {
        return text.IsNullOrEmpty() || text.IsNullOrWhiteSpace();
    }
}