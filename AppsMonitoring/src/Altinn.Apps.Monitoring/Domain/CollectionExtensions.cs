namespace System.Linq;

public static class CollectionExtensions
{
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source)
    {
        foreach (var item in source)
        {
            if (item is not null)
            {
                yield return item;
            }
        }
    }

    public static async IAsyncEnumerable<T> WhereNotNull<T>(this IAsyncEnumerable<T?> source)
    {
        await foreach (var item in source)
        {
            if (item is not null)
            {
                yield return item;
            }
        }
    }
}
