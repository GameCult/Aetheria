using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

public static class CollectionExtensions
{
    // https://stackoverflow.com/a/800469
    public static string GetHashSHA1(this byte[] data)
    {
        using (var sha1 = new System.Security.Cryptography.SHA1CryptoServiceProvider())
        {
            return string.Concat(sha1.ComputeHash(data).Select(x => x.ToString("X2")));
        }
    }

    // https://stackoverflow.com/a/3188835
    public static T MaxBy<T, U>(this IEnumerable<T> items, Func<T, U> selector)
    {
        if (!items.Any())
        {
            throw new InvalidOperationException("Empty input sequence");
        }

        var comparer = Comparer<U>.Default;
        T   maxItem  = items.First();
        U   maxValue = selector(maxItem);

        foreach (T item in items.Skip(1))
        {
            // Get the value of the item and compare it to the current max.
            U value = selector(item);
            if (comparer.Compare(value, maxValue) > 0)
            {
                maxValue = value;
                maxItem  = item;
            }
        }

        return maxItem;
    }
    
    public static T MinBy<T, U>(this IEnumerable<T> items, Func<T, U> selector)
    {
        if (!items.Any())
        {
            throw new InvalidOperationException("Empty input sequence");
        }

        var comparer = Comparer<U>.Default;
        T   maxItem  = items.First();
        U   maxValue = selector(maxItem);

        foreach (T item in items.Skip(1))
        {
            // Get the value of the item and compare it to the current max.
            U value = selector(item);
            if (comparer.Compare(value, maxValue) < 0)
            {
                maxValue = value;
                maxItem  = item;
            }
        }

        return maxItem;
    }
}

public static class AsyncAwaitExtensions
{
    public static async void WrapAwait(this Task task)
    {
        await task;
    }
}