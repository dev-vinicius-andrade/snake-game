namespace Domain.Game.Extensions;

public static class ListExtensions
{
    public static IList<T> AddValue<T>(this IEnumerable<T> enumerable, T entity)
    {
        var list = enumerable.ToList();
        list.Add(entity);
        return list;
    }
}