using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace DeepObjectDiff
{
    /// <summary>
    /// Contains helpers used when working with <see cref="IEnumerable{T}"/>
    /// </summary>
    internal static class EnumerableExtensionHelper
    {
        /// <summary>
        /// Works like <see cref="System.Linq.Enumerable.Zip{TFirst,TSecond,TResult}"/>, however it will continue using <code>default()</code> if one of the enumerations ends before another.
        /// </summary>
        /// <typeparam name="TFirst">Type of the first enumeration</typeparam>
        /// <typeparam name="TSecond">Type of the second enumerration</typeparam>
        /// <typeparam name="TResult">Type of the result of zipping</typeparam>
        /// <param name="first">First enumeration to zip</param>
        /// <param name="second">Second enumeration to zip</param>
        /// <param name="resultSelector">Func that merges elements of <typeparamref name="TFirst"/> and <typeparamref name="TSecond"/> to create <typeparamref name="TResult"/></param>
        /// <returns></returns>
        internal static IEnumerable<TResult> ZipFullLength<TFirst, TSecond, TResult>([NotNull] this IEnumerable<TFirst> first,
            [NotNull] IEnumerable<TSecond> second, [NotNull] Func<TFirst, TSecond, TResult> resultSelector)
        {
            if (first == null) throw new ArgumentNullException(nameof(first));
            if (second == null) throw new ArgumentNullException(nameof(second));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));

            using (var firstE = first.GetEnumerator())
            using (var secondE = second.GetEnumerator())
            {
                bool firstHas, secondHas;
                do
                {
                    firstHas = firstE.MoveNext();
                    secondHas = secondE.MoveNext();

                    if (!(firstHas || secondHas))
                        continue;

                    var firstElement = firstHas ? firstE.Current : default;
                    var secondElement = secondHas ? secondE.Current : default;
                    
                    yield return resultSelector(firstElement, secondElement);

                } while (firstHas || secondHas);
            }
        }
    }
}