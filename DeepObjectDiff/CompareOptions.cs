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

        public StringComparison DefaultStringComparison { get; [PublicAPI] set; } = StringComparison.Ordinal;
        public ICollection<Type> TypesToIgnore { get; [PublicAPI] set; } = new Type[0];
        public ICollection<string> PathsToIgnore { get; [PublicAPI] set; } = new string[0];
        public bool UseMultithreading { get; [PublicAPI] set; }
        public bool VerifyListOrder { get; [PublicAPI] set; }
        public bool EnumerateEnumerables { get; [PublicAPI]set; }
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

        internal CompareOptionsInternal(CompareOptions options)
        {
            EqualityComparers = options.EqualityComparers;
            DefaultStringComparison = options.DefaultStringComparison;
            UseMultithreading = options.UseMultithreading;
            VerifyListOrder = options.VerifyListOrder;
            EnumerateEnumerables = options.EnumerateEnumerables;

            TypesToIgnore = new HashSet<Type>(options.TypesToIgnore);
            PathsToIgnore = new HashSet<string>(options.PathsToIgnore);
        }


    }
}