using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Backend.Util
{
    public static class EnumerableExtensions
    {
        public static async Task<IEnumerable<TResult>> SelectManyAsync<TSource, TResult>(
            this IEnumerable<TSource> source, Func<TSource, Task<IEnumerable<TResult>>> selector)
        {
            var results = await Task.WhenAll(source.Select(selector));
            return results.SelectMany(r => r);
        }
    }
}