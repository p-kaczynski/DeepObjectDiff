using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace DeepObjectDiff
{
    public class CompareOptions
    {
        internal IDictionary<Type, object> EqualityComparers { get; } = new Dictionary<Type, object>();

        [PublicAPI]
        public CompareOptions Use<T>(IEqualityComparer<T> comparer)
        {
            EqualityComparers.Add(typeof(T), comparer);
            return this;
        }

        /// <summary>
        ///  <see cref="StringComparison"/> mode to use as default, default is <see cref="StringComparison.Ordinal"/>
        /// </summary>
        public StringComparison DefaultStringComparison { get; [PublicAPI] set; } = StringComparison.Ordinal;

        /// <summary>
        /// Types that should not be compared
        /// </summary>
        public ICollection<Type> TypesToIgnore { get; [PublicAPI] set; } = new Type[0];

        /// <summary>
        /// Paths that should be ignored in the format of '/propertyName/propertyName'
        /// </summary>
        public ICollection<string> PathsToIgnore { get; [PublicAPI] set; } = new string[0];

        /// <summary>
        /// Whether to use Multithreading in recursion and collection checks where possible, default false
        /// </summary>
        /// <remarks>
        /// Multithreading is not currently implemented and this setting has no effect
        /// </remarks>
        public bool UseMultithreading { get; [PublicAPI] set; } = false;

        /// <summary>
        /// Whether to verify that lists not only have equal content, but also have it in the same order (default: true)
        /// </summary>
        public bool VerifyListOrder { get; [PublicAPI] set; } = true;
        
        /// <summary>
        /// Whether to enumerate all elements in enumerations and compare them (default) or ignore them
        /// </summary>
        public bool EnumerateEnumerables { get; [PublicAPI] set; } = true;

        /// <summary>
        /// Whether to stop at first difference found (default), or continue deeper to find all differences
        /// </summary>
        public bool StopAtFirstDifference { get; [PublicAPI] set; } = true;
    }

    internal class CompareOptionsInternal
    {
        internal IDictionary<Type, object> EqualityComparers { get; }
        internal StringComparison DefaultStringComparison { get; }
        internal ISet<Type> TypesToIgnore { get; }
        internal ISet<string> PathsToIgnore { get; }
        internal bool UseMultithreading { get; }
        internal bool VerifyListOrder { get; }
        internal bool EnumerateEnumerables { get; }
        internal bool StopAtFirstDifference { get; }

        internal CompareOptionsInternal(CompareOptions options)
        {
            EqualityComparers = options.EqualityComparers;
            DefaultStringComparison = options.DefaultStringComparison;
            UseMultithreading = options.UseMultithreading;
            VerifyListOrder = options.VerifyListOrder;
            EnumerateEnumerables = options.EnumerateEnumerables;
            StopAtFirstDifference = options.StopAtFirstDifference;

            TypesToIgnore = new HashSet<Type>(options.TypesToIgnore);
            PathsToIgnore = new HashSet<string>(options.PathsToIgnore, StringComparer.OrdinalIgnoreCase);
        }


    }
}