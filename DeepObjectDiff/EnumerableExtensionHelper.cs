using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace DeepObjectDiff
{
    internal static class EnumerableExtensionHelper
    {
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

                    var firstElement = firstHas ? firstE.Current : default(TFirst);
                    var secondElement = secondHas ? secondE.Current : default(TSecond);
                    
                    yield return resultSelector(firstElement, secondElement);

                } while (firstHas || secondHas);
            }
        }
    }
}