// Copyright (c) Pomelo Foundation. All rights reserved.
// Copyright (c) SingleStore Inc. All rights reserved.
// Licensed under the MIT. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace System.Collections.Generic
{
    [DebuggerStepThrough]
    internal static class EnumerableExtensions
    {
        public static string Join(this IEnumerable<object> source, string separator = ", ")
            => string.Join(separator, source);

        public static IEnumerable<T> Distinct<T>(
            this IEnumerable<T> source, Func<T, T, bool> comparer)
            where T : class
            => source.Distinct(new DynamicEqualityComparer<T>(comparer));

        public static async Task<List<TSource>> ToListAsync<TSource>(
            this IAsyncEnumerable<TSource> source,
            CancellationToken cancellationToken = default)
        {
            var list = new List<TSource>();
            await foreach (var element in source.WithCancellation(cancellationToken))
            {
                list.Add(element);
            }

            return list;
        }

        private sealed class DynamicEqualityComparer<T> : IEqualityComparer<T>
            where T : class
        {
            private readonly Func<T, T, bool> _func;

            public DynamicEqualityComparer(Func<T, T, bool> func)
            {
                _func = func;
            }

            public bool Equals(T x, T y) => _func(x, y);

            public int GetHashCode(T obj) => 0; // force Equals
        }
    }
}
