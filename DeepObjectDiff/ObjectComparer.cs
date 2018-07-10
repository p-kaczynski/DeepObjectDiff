using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DeepObjectDiff
{
    public static class ObjectComparer
    {
        public static ObjectDifference[] Compare<T>(T first, T second, CompareOptions options = null)
        {
            options = options ?? new CompareOptions();

            return Compare<T>(first, second, new CompareOptionsInternal(options), new PropertyProvider(), new ComparisonContext()).ToArray();
        }

        private static IEnumerable<ObjectDifference> Compare<T>(T first, T second, CompareOptionsInternal options,
            PropertyProvider provider, ComparisonContext comparisonContext)
        {
            // 1. Reference check
            if (ReferenceEquals(first, second))
                yield break; // no need to go deeper, they are at this point SAME object

            // 2. Null checks
            if (first == null || second == null)
            {
                // oops, we know that they are not BOTH nulls due to check #1
                yield return new ObjectDifference(comparisonContext.GetCurrentPath(), first, second);
                yield break; // no need to go deeper, everything deeper down will be a difference, as one of them is null, but not second
            }
            

            // 3. Do we have an explicit comparer for this object?
            if (options.EqualityComparers.TryGetValue(typeof(T), out var equalityComparerObj)
                && equalityComparerObj is IEqualityComparer<T> equalityComparer
                && !equalityComparer.Equals(first, second))
            {
                yield return new ObjectDifference(comparisonContext.GetCurrentPath(), first, second);
                yield break;
            }

            // 4. Is the object equatable?
            if (first is IEquatable<T> equatableFirst
                && !equatableFirst.Equals(second))
            {
                // The type implements IEquatable to itself, so any sub-level comparisons are delegated to this method
                yield return new ObjectDifference(comparisonContext.GetCurrentPath(), first, second);
                yield break; // we could go deeper, but we know that the objects are fundamentally different
            }

            // 5. Is the object a collection?
            // 5.1 IDictionary
            if (typeof(T).TryAsGenericDictionary(out var keyType, out var valueType))
            {
                // TODO: cache?
                foreach (var difference in
                    CallCollectionComparer(nameof(CompareDictionaries), first, second, options,
                        provider, comparisonContext, keyType, valueType))
                {
                    yield return difference;
                }

                yield break; // we done here
            }
            // 5.2 Set
            if (typeof(T).TryAsGenericSet(out var elementType))
            {
                foreach (var difference in
                    CallCollectionComparer(nameof(CompareSet), first, second, options,
                        provider, comparisonContext, elementType))
                {
                    yield return difference;
                }

                yield break; // we done here
            }

            // 5.3 List
            if (typeof(T).TryAsGenericList(out elementType))
            {
                foreach (var difference in
                    CallCollectionComparer(nameof(CompareList), first, second, options,
                        provider, comparisonContext, elementType))
                {
                    yield return difference;
                }

                yield break; // we done here
            }

            // 5.4 Enumerable
            if (typeof(T).TryAsGenericEnumerable(out elementType))
            {
                foreach (var difference in
                    CallCollectionComparer(nameof(CompareEnumerable), first, second, options,
                        provider, comparisonContext, elementType))
                {
                    yield return difference;
                }

                yield break; // we done here
            }

            // 6. Not enumerable, we don't know how to check it - 
            var properties = provider.GetAllProperties<T>();

        }

        private static IEnumerable<ObjectDifference> CallCollectionComparer(string methodName, object first, object second,
            CompareOptionsInternal options,
            PropertyProvider provider, ComparisonContext comparisonContext, params Type[] genericArguments)
        {
            var method = typeof(ObjectComparer)
                             .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static) ?? throw new ArgumentNullException($"typeof(ObjectComparer).GetMethod({methodName}, BindingFlags.NonPublic | BindingFlags.Static)");

            return (IEnumerable<ObjectDifference>) method
                .MakeGenericMethod(genericArguments)
                .Invoke(null, new[] {first, second, options, provider, comparisonContext});
        }

        private static IEnumerable<ObjectDifference> CompareDictionaries<TKey, TValue>(IDictionary<TKey, TValue> first,
            IDictionary<TKey, TValue> second, CompareOptionsInternal options,
            PropertyProvider provider, ComparisonContext comparisonContext)
        {
            throw new NotImplementedException();
        }

        private static IEnumerable<ObjectDifference> CompareSets<TElement>(ISet<TElement> first,
            ISet<TElement> second, CompareOptionsInternal options,
            PropertyProvider provider, ComparisonContext comparisonContext)
        {
            if (first.SetEquals(second)) yield break;
            var firstCopy = new HashSet<TElement>(first,);
            firstCopy.SymmetricExceptWith(second);
        }

        private class ProxyEqualityComparer<T> : IEqualityComparer<T>
        {
            public ProxyEqualityComparer(CompareOptionsInternal options,
                PropertyProvider provider, ComparisonContext comparisonContext)
            {
            }

            public bool Equals(T x, T y)
            {
                throw new NotImplementedException();
            }

            public int GetHashCode(T obj) => obj.GetHashCode();
        }
    }
}