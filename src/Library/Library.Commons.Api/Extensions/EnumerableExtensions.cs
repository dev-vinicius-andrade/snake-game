namespace Library.Commons.Api.Extensions;

public static class EnumerableExtensions
{
    
    public static bool IsNullOrEmpty<T>(this IEnumerable<T> source)
    {
        if (source == null) return true;
        return !source.Any();
    }
    public static bool IsNullOrEmpty<T>(this T[] array)
    {
        return array == null || array.Length == 0;
    }
}