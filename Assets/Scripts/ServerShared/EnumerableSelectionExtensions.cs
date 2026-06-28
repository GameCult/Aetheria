using System;
using System.Collections.Generic;
using System.Linq;

public static class EnumerableSelectionExtensions
{
    public static T MaxBy<T, U>(this IEnumerable<T> items, Func<T, U> selector)
    {
        if (!items.Any())
        {
            throw new InvalidOperationException("Empty input sequence");
        }

        var comparer = Comparer<U>.Default;
        var maxItem = items.First();
        var maxValue = selector(maxItem);

        foreach (var item in items.Skip(1))
        {
            var value = selector(item);
            if (comparer.Compare(value, maxValue) > 0)
            {
                maxValue = value;
                maxItem = item;
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
        var minItem = items.First();
        var minValue = selector(minItem);

        foreach (var item in items.Skip(1))
        {
            var value = selector(item);
            if (comparer.Compare(value, minValue) < 0)
            {
                minValue = value;
                minItem = item;
            }
        }

        return minItem;
    }
}
