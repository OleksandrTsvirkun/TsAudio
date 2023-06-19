using System;
using System.Collections.Generic;
using System.Numerics;

namespace TsAudio.Utils;
public static class ListExtensions
{
    public static bool IsNullOrEmpty<T>(this IReadOnlyCollection<T> collection)
    {
        return collection is null || collection.Count == 0;
    }

    public static int IndexOfNear<T, TValue>(this IReadOnlyList<T> list, TValue value, Func<T, TValue> selector)
        where TValue : IComparisonOperators<TValue, TValue, bool>
    {
        var minIndex = 0;
        var maxIndex = list.Count - 1;
        var midIndex = (minIndex + maxIndex) / 2;

        while(minIndex <= maxIndex)
        {
            midIndex = (minIndex + maxIndex) / 2;

            if(value < selector(list[midIndex]))
            {
                maxIndex = midIndex - 1;
            }
            else
            {
                minIndex = midIndex + 1;
            }
        }

        return midIndex;
    }
}
