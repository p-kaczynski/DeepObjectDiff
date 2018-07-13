using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

namespace DeepObjectDiff
{
    /// <summary>
    /// Universal comparer of objects. Use <see cref="CompareOptions"/> to customize the behaviour.
    /// </summary>
    public static class ObjectComparer
    {
        /// <summary>
        /// Performs a deep object comparison between <paramref name="first"/> and <see cref="second"/> by first trying conventional means, and if those do not give clear answer,
        /// traverses down the object graph to determine whether the objects are equivalent as understood by settings provided in <see cref="CompareOptions"/>.
        /// Additionally it provides encountered differences along with their path in the object graph in an out parameter <paramref name="differences"/>
        /// </summary>
        /// <typeparam name="T">Type of objects to compare.</typeparam>
        /// <param name="first">First object of comparison</param>
        /// <param name="second">Second object of comparison to compare with <paramref name="first"/></param>
        /// <param name="differences">Out parameter set to contain encoutnered differences between <paramref name="first"/> and <paramref name="second"/>, if any</param>
        /// <param name="options">(optional) User-provided settings that customise behaviour of the comparison</param>
        /// <returns><c>true</c> if objects are equivalent as understood by provided <paramref name="options"/> (or default <see cref="CompareOptions"/>), otherwise <c>false</c></returns>
        [PublicAPI]
        public static bool Compare<T>([CanBeNull]T first, [CanBeNull]T second, out ObjectDifference[] differences, [CanBeNull]CompareOptions options = null)
        {
            // Use default options if not provide by users
            options = options ?? new CompareOptions();

            // Create a new context
            var context = new ComparisonContext();

            // Run actual compare functions, it will call itself if needed for collections/properties/etc.
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
                //    We are actually going to get the properties and compare them one to one
                var properties = provider.GetAllProperties<T>();

                var propertyEqual = true;
                foreach (var propertyInfo in properties)
                {
                    // This will note that we have entered a property of given name
                    comparisonContext.Enter(propertyInfo.Name);

                    // TODO: add multithreading, remember to clone context to prevent messing up the path stacks
                    propertyEqual = CallCompareFor(propertyInfo.PropertyType, propertyInfo.GetValue(first),
                        propertyInfo.GetValue(second), options, provider, comparisonContext);

                    // Unless full diff requested by user - bail, we have our answer
                    if (!propertyEqual && options.StopAtFirstDifference)
                        return false;

                    // property comparison finished, remove from stack
                    comparisonContext.Exit();
                }

                return propertyEqual;
            }
            finally
            {
                // ALWAYS run this, because if we got to this point we have called TryEnterObject (and it returned true).
                comparisonContext.ExitObject<T>();
            }
        }

        /// <summary>
        /// A helper that allows calling the <see cref="Compare{T}"/> method even if we can't explicitly provide type T
        /// </summary>
        /// <param name="compareType">Type of the objects to compare</param>
        /// <param name="first">First object of comparison</param>
        /// <param name="second">Second object of comparison, to be compared with <paramref name="first"/></param>
        /// <param name="options">Options of the comparison</param>
        /// <param name="provider">PropertyProvider, reused to take advantage of caching</param>
        /// <param name="comparisonContext">Current context of the comparison</param>
        /// <returns>Result of <see cref="Compare{T}"/> method called with above parameters</returns>
        private static bool CallCompareFor(Type compareType, object first, object second,
            CompareOptionsInternal options, PropertyProvider provider, ComparisonContext comparisonContext)
        {
            // We are using nameof to prevent nasty typos
            var method = typeof(ObjectComparer).GetMethod(nameof(Compare), BindingFlags.Static | BindingFlags.NonPublic)
                         ?? throw new ArgumentNullException(
                             $"typeof(ObjectComparer).GetMethod({nameof(Compare)}, BindingFlags.Static|BindingFlags.NonPublic)");

            // This whole thing is so we can do MakeGenericMethod - normally for Method<T>, T must be eitehr explicitly stated, or passed as dynamic for runtime binding, however we know the type - only it's in a variable
            return (bool) method
                .MakeGenericMethod(compareType)
                .Invoke(null, new[] {first, second, options, provider, comparisonContext}); // we pass the parameters as needed by the Compare method
        }

        /// <summary>
        /// A helper that allows calling one of the generic collection comparers while having the type in a variable
        /// </summary>
        /// <param name="methodName">Name of the collection comparer to use</param>
        /// <param name="first">First object of comparison</param>
        /// <param name="second">Second object of comparison, to be compared with <paramref name="first"/></param>
        /// <param name="options">Options of the comparison</param>
        /// <param name="provider">PropertyProvider, reused to take advantage of caching</param>
        /// <param name="comparisonContext">Current context of the comparison</param>
        /// <param name="genericArguments">Generic arguments to pass to the methods (either element type, or key/value types for <see cref="IDictionary{TKey,TValue}"/>)</param>
        /// <returns>Result of called comparer</returns>
        private static bool CallCollectionComparer(string methodName, object first, object second,
            CompareOptionsInternal options,
            PropertyProvider provider, ComparisonContext comparisonContext, params Type[] genericArguments)
        {
            // As with CallCompareFor we use name of the method to retrive it from this very class
            var method = typeof(ObjectComparer)
                             .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static) ??
                         throw new ArgumentNullException(
                             $"typeof(ObjectComparer).GetMethod({methodName}, BindingFlags.NonPublic | BindingFlags.Static)");

            // And again, call generic method while having type argument(s) in a variable
            return (bool) method
                .MakeGenericMethod(genericArguments)
                .Invoke(null, new[] {first, second, options, provider, comparisonContext});
        }

        /// <summary>
        /// Compares two <see cref="IDictionary{TKey,TValue}"/> objects to determine whether they have the same keys and contain the same objects under said keys
        /// </summary>
        /// <typeparam name="TKey">Key type of the <see cref="IDictionary{TKey,TValue}"/></typeparam>
        /// <typeparam name="TValue">Value type of the <see cref="IDictionary{TKey,TValue}"/></typeparam>
        /// <param name="first">First dictionary for comparison</param>
        /// <param name="second">Second dictionary for comparison, to be compared with <paramref name="first"/></param>
        /// <param name="options">Options of the comparison</param>
        /// <param name="provider">PropertyProvider, reused to take advantage of caching</param>
        /// <param name="comparisonContext">Current context of the comparison</param>
        /// <returns><c>true</c> if dictionaries contain the same objects under the same keys, otherwise <c>false</c></returns>
        private static bool CompareDictionaries<TKey, TValue>(IDictionary<TKey, TValue> first,
            IDictionary<TKey, TValue> second, CompareOptionsInternal options,
            PropertyProvider provider, ComparisonContext comparisonContext)
        {
            var equal = true;

            // NOTE: this indeed could be simpler if we cared only about equivalence, but we might also want all the differences

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

                if (!equal && options.StopAtFirstDifference)
                    return false;
            }

            return equal;
        }

        /// <summary>
        /// Compares two <see cref="ISet{T}"/> objects to determine if they contain the same objects (by the same rules as <see cref="Compare{T}"/> method)
        /// </summary>
        /// <typeparam name="TElement">Type of the <see cref="ISet{T}"/> element</typeparam>
        /// <param name="first">First set to compare</param>
        /// <param name="second">Second set to compare with <see cref="first"/></param>
        /// <param name="options">Options of the comparison</param>
        /// <param name="provider">PropertyProvider, reused to take advantage of caching</param>
        /// <param name="comparisonContext">Current context of the comparison</param>
        /// <returns><c>true</c> if sets contain the same objects, otherwise <c>false</c></returns>
        // ReSharper disable SuggestBaseTypeForParameter - I know it doesn't need to be a set, because we rewrite it into another set anyway, but it communicates the purpose better and it does not mess with MakeGenericMethod
        private static bool CompareSet<TElement>(ISet<TElement> first,
            ISet<TElement> second, CompareOptionsInternal options,
            PropertyProvider provider, ComparisonContext comparisonContext)
            => CompareAsSets(first, second, options, provider, comparisonContext);
        // ReSharper enable SuggestBaseTypeForParameter

        /// <summary>
        /// Compares two <see cref="IList{T}"/> objects to determine if they contain the same objects (by the same rules as <see cref="Compare{T}"/> method), and possibly in the same order, as determined by <paramref name="options"/>
        /// </summary>
        /// <typeparam name="TElement">Type of the <see cref="IList{T}"/> element</typeparam>
        /// <param name="first">First list to compare</param>
        /// <param name="second">Second list to compare with <see cref="first"/></param>
        /// <param name="options">Options of the comparison</param>
        /// <param name="provider">PropertyProvider, reused to take advantage of caching</param>
        /// <param name="comparisonContext">Current context of the comparison</param>
        /// <returns><c>true</c> if lists contain the same objects (and possibly in the same order as determined by <paramref name="options"/>), otherwise <c>false</c></returns>
        private static bool CompareList<TElement>(IList<TElement> first,
            IList<TElement> second, CompareOptionsInternal options,
            PropertyProvider provider, ComparisonContext comparisonContext) 
            => options.VerifyListOrder 
                ? CompareAsEnumeration(first, second, options, provider, comparisonContext) 
                : CompareAsSets(first, second, options, provider, comparisonContext); // use set approach if they can be anywhere (order insensitive)

        /// <summary>
        /// Compares two <see cref="IEnumerable{T}"/> objects to determine if they contain the same objects (by the same rules as <see cref="Compare{T}"/> method) in the same order
        /// </summary>
        /// <typeparam name="TElement">Type of the <see cref="IEnumerable{T}"/> element</typeparam>
        /// <param name="first">First enumerabe to compare</param>
        /// <param name="second">Second enumerable to compare with <see cref="first"/></param>
        /// <param name="options">Options of the comparison</param>
        /// <param name="provider">PropertyProvider, reused to take advantage of caching</param>
        /// <param name="comparisonContext">Current context of the comparison</param>
        /// <returns><c>true</c> if enumerable contain the same objects in the same order, otherwise <c>false</c></returns>
        private static bool CompareEnumerable<TElement>(IEnumerable<TElement> first,
            IEnumerable<TElement> second, CompareOptionsInternal options,
            PropertyProvider provider, ComparisonContext comparisonContext)
                => !options.EnumerateEnumerables // we might be prohibited from enumerating enumerables
                    || CompareAsEnumeration(first, second, options, provider, comparisonContext); // if not, we use the zip approach


        /// <summary>
        /// Compares two <see cref="ICollection{T}"/> objects to determine if they contain the same objects (by the same rules as <see cref="Compare{T}"/> method)
        /// It treats the collections as sets, i.e. does not care about order, just about containing the same objects.
        /// </summary>
        /// <typeparam name="TElement">Type of the <see cref="ICollection{T}"/> element</typeparam>
        /// <param name="first">First collection to compare</param>
        /// <param name="second">Second collection to compare with <see cref="first"/></param>
        /// <param name="options">Options of the comparison</param>
        /// <param name="provider">PropertyProvider, reused to take advantage of caching</param>
        /// <param name="comparisonContext">Current context of the comparison</param>
        /// <returns><c>true</c> if collections contain the same objects, otherwise <c>false</c></returns>
        /// <returns></returns>
        private static bool CompareAsSets<TElement>(ICollection<TElement> first, ICollection<TElement> second,
            CompareOptionsInternal options, PropertyProvider provider, ComparisonContext comparisonContext)
        {
            // We first look at the difference between first and second
            var firstCompare = new HashSet<TElement>(first, new ProxyEqualityComparer<TElement>(options, provider, comparisonContext));
            firstCompare.ExceptWith(second);
            foreach (var difference in firstCompare)
                comparisonContext.AddDifferenceAtCurrentPath(difference, null);

            // and then at the difference between second and first
            var secondCompare = new HashSet<TElement>(second, new ProxyEqualityComparer<TElement>(options, provider, comparisonContext));
            secondCompare.ExceptWith(first);
            foreach (var difference in secondCompare)
                comparisonContext.AddDifferenceAtCurrentPath(null, difference);

            // NOTE: SymmetricExceptWith COULD be used, but we either would loose the information about which tree (first, second) specific objects exist/don't exist in, or we would have to run 'Contains' checks for each

            // Equal if neither of comparison collections has any elements
            return !(firstCompare.Any() || secondCompare.Any());
        }

        /// <summary>
        /// Compares two <see cref="IEnumerable{T}"/> objects to determine if they contain the same objects (by the same rules as <see cref="Compare{T}"/> method) in the same order
        /// </summary>
        /// <typeparam name="TElement">Type of the <see cref="IEnumerable{T}"/> element</typeparam>
        /// <param name="first">First enumerabe to compare</param>
        /// <param name="second">Second enumerable to compare with <see cref="first"/></param>
        /// <param name="options">Options of the comparison</param>
        /// <param name="provider">PropertyProvider, reused to take advantage of caching</param>
        /// <param name="comparisonContext">Current context of the comparison</param>
        /// <returns><c>true</c> if enumerable contain the same objects in the same order, otherwise <c>false</c></returns>
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


        /// <summary>
        /// A helper map of <see cref="StringComparison"/> to <see cref="StringComparer"/> that uses those settings
        /// </summary>
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

        /// <inheritdoc />
        /// <summary>
        /// An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation that uses <see cref="M:DeepObjectDiff.ObjectComparer.Compare``1(``0,``0,DeepObjectDiff.ObjectDifference[]@,DeepObjectDiff.CompareOptions)" /> method to determine equality (or rather equivalence)
        /// </summary>
        /// <typeparam name="T">Type of the objects to compare</typeparam>
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
                
                // we clone the context, as in multithreaded scenario this would behave very weirdly
                _comparisonContext = (ComparisonContext) comparisonContext.Clone();

                // and we MUST NOT generate differences, because those are unavoidable during collection comparison, and don't actually mean the entire collections are not equivalent
                _comparisonContext.PreventDifferenceGeneration = true;
            }

            public bool Equals(T x, T y) => Compare(x, y, _options, _provider, _comparisonContext);

            public int GetHashCode(T obj) => obj.GetHashCode();
        }
    }
}