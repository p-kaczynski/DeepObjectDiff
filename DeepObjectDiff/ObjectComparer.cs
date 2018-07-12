using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

namespace DeepObjectDiff
{
    public static class ObjectComparer
    {
        [PublicAPI]
        public static bool Compare<T>([CanBeNull]T first, [CanBeNull]T second, out ObjectDifference[] differences, [CanBeNull]CompareOptions options = null)
        {
            options = options ?? new CompareOptions();
            var context = new ComparisonContext();

            var equal = Compare(first, second, new CompareOptionsInternal(options), new PropertyProvider(),
                context);

            differences = context.Differences;

            return equal;
        }

        private static bool Compare<T>(T first, T second, CompareOptionsInternal options,
            PropertyProvider provider, ComparisonContext comparisonContext)
        {
            // 0. Initial checks
            // 0.1 Recursion check
            if(!comparisonContext.TryEnterObject(first, second))
                throw new InvalidOperationException($"Detected a circular reference at {comparisonContext.GetCurrentPath()}. Add the property to the blacklist to compare those objects");
            try
            {
                // 0.2 Type blacklist check
                if (options.TypesToIgnore.Contains(typeof(T)))
                    return true; // we don't care

                // 0.3 Path blacklist check
                if (options.PathsToIgnore.Contains(comparisonContext.GetCurrentPath()))
                    return true;

                // 1. Reference check
                if (ReferenceEquals(first, second))
                    return true; // no need to go deeper, they are at this point SAME object

                // 2. Null checks
                if (first == null || second == null)
                {
                    // oops, we know that they are not BOTH nulls due to check #1
                    comparisonContext.AddDifferenceAtCurrentPath(first, second);
                    return
                        false; // no need to go deeper, everything deeper down will be a difference, as one of them is null, but not second
                }


                // 3. Do we have an explicit comparer for this object?
                if (options.EqualityComparers.TryGetValue(typeof(T), out var equalityComparerObj)
                    && equalityComparerObj is IEqualityComparer<T> equalityComparer)
                {
                    if (equalityComparer.Equals(first, second))
                        return true;

                    comparisonContext.AddDifferenceAtCurrentPath(first, second);
                    return false;
                }

                // 3.1 Maybe it's strings?
                if (first is string firstString
                    && second is string secondString)
                {
                    if (string.Equals(firstString, secondString, options.DefaultStringComparison))
                        return true;

                    comparisonContext.AddDifferenceAtCurrentPath(first, second);
                    return false;
                }

                // 4. Is the object equatable?
                if (first is IEquatable<T> equatableFirst)
                {
                    // The type implements IEquatable to itself, so any sub-level comparisons are delegated to this method
                    if (equatableFirst.Equals(second))
                        return true;

                    comparisonContext.AddDifferenceAtCurrentPath(first, second);
                    return false;
                }

                // 5. Is the object a collection?
                // 5.1 IDictionary
                if (typeof(T).TryAsGenericDictionary(out var keyType, out var valueType))
                {
                    // TODO: cache?
                    return CallCollectionComparer(nameof(CompareDictionaries), first, second, options,
                        provider, comparisonContext, keyType, valueType);


                    //return false;
                }

                // 5.2 Set
                if (typeof(T).TryAsGenericSet(out var elementType))
                {
                    return CallCollectionComparer(nameof(CompareSet), first, second, options,
                        provider, comparisonContext, elementType);

                }

                // 5.3 List
                if (typeof(T).TryAsGenericList(out elementType))
                {
                    return CallCollectionComparer(nameof(CompareList), first, second, options,
                        provider, comparisonContext, elementType);

                }

                // 5.4 Enumerable
                if (typeof(T).TryAsGenericEnumerable(out elementType))
                {
                    return CallCollectionComparer(nameof(CompareEnumerable), first, second, options,
                        provider, comparisonContext, elementType);

                }

                // 6. Not enumerable, we don't know how to check it - now the fun starts.
                //    We are actually going to get the properties and
                var properties = provider.GetAllProperties<T>();
                bool propertyEqual = true;
                foreach (var propertyInfo in properties)
                {
                    comparisonContext.Enter(propertyInfo.Name);

                    propertyEqual = CallCompareFor(propertyInfo.PropertyType, propertyInfo.GetValue(first),
                        propertyInfo.GetValue(second), options, provider, comparisonContext);

                    if (!propertyEqual && options.StopAtFirstDifference)
                        return false;

                    comparisonContext.Exit();
                }

                return propertyEqual;
            }
            finally
            {
                comparisonContext.ExitObject<T>();
            }
        }

        private static bool CallCompareFor(Type compareType, object first, object second,
            CompareOptionsInternal options, PropertyProvider provider, ComparisonContext comparisonContext)
        {
            var method = typeof(ObjectComparer).GetMethod(nameof(Compare), BindingFlags.Static | BindingFlags.NonPublic)
                         ?? throw new ArgumentNullException(
                             $"typeof(ObjectComparer).GetMethod({nameof(Compare)}, BindingFlags.Static|BindingFlags.NonPublic)");

            return (bool) method
                .MakeGenericMethod(compareType)
                .Invoke(null, new[] {first, second, options, provider, comparisonContext});
        }

        private static bool CallCollectionComparer(string methodName, object first, object second,
            CompareOptionsInternal options,
            PropertyProvider provider, ComparisonContext comparisonContext, params Type[] genericArguments)
        {
            var method = typeof(ObjectComparer)
                             .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static) ??
                         throw new ArgumentNullException(
                             $"typeof(ObjectComparer).GetMethod({methodName}, BindingFlags.NonPublic | BindingFlags.Static)");

            return (bool) method
                .MakeGenericMethod(genericArguments)
                .Invoke(null, new[] {first, second, options, provider, comparisonContext});
        }

        private static bool CompareDictionaries<TKey, TValue>(IDictionary<TKey, TValue> first,
            IDictionary<TKey, TValue> second, CompareOptionsInternal options,
            PropertyProvider provider, ComparisonContext comparisonContext)
        {
            var equal = true;

            // TODO: this smells superfluous as it repeates the logic from Compare method
            var keys = first.Keys.Concat(second.Keys);
            // if we can distinct the keys by any known means, we should
            if (options.EqualityComparers.TryGetValue(typeof(TKey), out var keyComparerObj)
                && keyComparerObj is IEqualityComparer<TKey> keyComparer)
                keys = keys.Distinct(keyComparer); // we have our custom comparer to use
            else if (typeof(TKey) == typeof(string))
                keys = keys
                    .Cast<string>()
                    .Distinct(StringComparisonToStringComparer[
                        options.DefaultStringComparison]) // it's string, use string equality
                    .Cast<TKey>();
            else if (typeof(IEquatable<TKey>).IsAssignableFrom(typeof(TKey)))
                keys = keys.Distinct(); // we can use default, as it will use IEquatable interface

            // else: we can't distinct it, because we would have to run Compare on them and it would be painful
            // but fear not: the dictionaries have their own comparers they use, so whatever was used, we will use
            // the only downside: some keys, or even all keys could be duplicated - but well, this is only if you use some non-IEquatable keys

            foreach (var key in keys)
            {
                comparisonContext.Enter($"[{key}]");
                var firstHas = first.TryGetValue(key, out var firstValue);
                var secondHas = second.TryGetValue(key, out var secondValue);
                if (firstHas && secondHas)
                {
                    // This will save the difference for us
                    if (!Compare(firstValue, secondValue, options, provider, comparisonContext))
                        equal = false;
                }
                else
                {
                    equal = false;
                    // We could just use first/second value, but if type is int, and one dict has under 'key'
                    // value '0', then it would show as if both of them have 0, due to default(int) = 0
                    comparisonContext.AddDifferenceAtCurrentPath(
                        firstHas ? (object) firstValue : null,
                        secondHas ? (object) secondValue : null);
                }

                comparisonContext.Exit();
            }

            return equal;
        }

        private static bool CompareSet<TElement>(ISet<TElement> first,
            ISet<TElement> second, CompareOptionsInternal options,
            PropertyProvider provider, ComparisonContext comparisonContext)
            => CompareAsSets(first, second, options, provider, comparisonContext);



        private static bool CompareList<TElement>(IList<TElement> first,
            IList<TElement> second, CompareOptionsInternal options,
            PropertyProvider provider, ComparisonContext comparisonContext)
        {
            if (options.VerifyListOrder)
                return CompareAsEnumeration(first, second, options, provider, comparisonContext);

            // else use set approach if they can be anywhere (order insensitive)
            return CompareAsSets(first, second, options, provider, comparisonContext);

        }

        private static bool CompareEnumerable<TElement>(IEnumerable<TElement> first,
            IEnumerable<TElement> second, CompareOptionsInternal options,
            PropertyProvider provider, ComparisonContext comparisonContext)
        {
            if (!options.EnumerateEnumerables) return true; // we might be prohibited from enumerating enumerables

            // if not, we use the zip approach
            return CompareAsEnumeration(first, second, options, provider, comparisonContext);
        }

        private static readonly IReadOnlyDictionary<StringComparison, StringComparer> StringComparisonToStringComparer
            = new Dictionary<StringComparison, StringComparer>
            {
                {StringComparison.CurrentCulture, StringComparer.CurrentCulture},
                {StringComparison.CurrentCultureIgnoreCase, StringComparer.CurrentCultureIgnoreCase},
                {StringComparison.InvariantCulture, StringComparer.InvariantCulture},
                {StringComparison.InvariantCultureIgnoreCase, StringComparer.InvariantCultureIgnoreCase},
                {StringComparison.Ordinal, StringComparer.Ordinal},
                {StringComparison.OrdinalIgnoreCase, StringComparer.OrdinalIgnoreCase}
            };

        private static bool CompareAsSets<TElement>(ICollection<TElement> first, ICollection<TElement> second,
            CompareOptionsInternal options, PropertyProvider provider, ComparisonContext comparisonContext)
        {
            var firstCompare = new HashSet<TElement>(first, new ProxyEqualityComparer<TElement>(options, provider, comparisonContext));
            firstCompare.ExceptWith(second);
            foreach (var difference in firstCompare)
                comparisonContext.AddDifferenceAtCurrentPath(difference, null);

            var secondCompare = new HashSet<TElement>(second, new ProxyEqualityComparer<TElement>(options, provider, comparisonContext));
            secondCompare.ExceptWith(first);
            foreach (var difference in secondCompare)
                comparisonContext.AddDifferenceAtCurrentPath(null, difference);

            // Equal if neither of comparison collections has any elements
            return !(firstCompare.Any() || secondCompare.Any());
        }

        private static bool CompareAsEnumeration<TElement>(IEnumerable<TElement> first, IEnumerable<TElement> second,
            CompareOptionsInternal options, PropertyProvider provider, ComparisonContext comparisonContext)
        {
            // it will iterate at equal pace both lists, and run Compare - if a difference is found, it will be noted. Also, if nulls occur in a non-symmetric manner.
            var iterationContext = (ComparisonContext) comparisonContext.Clone();
            iterationContext.PreventDifferenceGeneration = true;
            return first
                .ZipFullLength(second, (f, s) => Compare(f, s, options, provider, iterationContext))
                .All(x => x);
        }

        private class ProxyEqualityComparer<T> : IEqualityComparer<T>
        {
            private readonly CompareOptionsInternal _options;
            private readonly PropertyProvider _provider;
            private readonly ComparisonContext _comparisonContext;

            public ProxyEqualityComparer(CompareOptionsInternal options, PropertyProvider provider,
                ComparisonContext comparisonContext)
            {
                _options = options;
                _provider = provider;
                _comparisonContext = (ComparisonContext) comparisonContext.Clone();
                _comparisonContext.PreventDifferenceGeneration = true;
            }

            public bool Equals(T x, T y) => Compare(x, y, _options, _provider, _comparisonContext);

            public int GetHashCode(T obj) => obj.GetHashCode();
        }
    }
}