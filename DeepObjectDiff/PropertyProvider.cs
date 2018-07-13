using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

namespace DeepObjectDiff
{
    /// <summary>
    ///     Provides a cached list of <see cref="PropertyInfo" />s for a particular <see cref="Type" />
    ///     This also centralises the logic of selecting which properties to include, and can be in the future expanded
    ///     to allow for configurable property selection.
    /// </summary>
    internal class PropertyProvider
    {
        /// <summary>
        ///     Cache of previously gathered <see cref="PropertyInfo" />s
        /// </summary>
        private readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> _cache =
            new ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>>();

        /// <summary>
        ///     Returns all selected <see cref="PropertyInfo" />s for a particular type <typeparamref name="T" />
        /// </summary>
        /// <typeparam name="T">Type for which to return selected properties</typeparam>
        /// <returns>Selected properties of type <typeparamref name="T" /></returns>
        [NotNull]
        internal IEnumerable<PropertyInfo> GetAllProperties<T>() => _cache.GetOrAdd(typeof(T), CacheType).Values;

        /// <summary>
        ///     Returns a particular <see cref="PropertyInfo" /> by its name for type <typeparamref name="T" />
        /// </summary>
        /// <typeparam name="T">Type for which to return</typeparam>
        /// <param name="name">Name of particular property</param>
        /// <returns>
        ///     If exists, <see cref="PropertyInfo" /> of a property by name of <paramref name="name" /> in type
        ///     <typeparamref name="T" />
        /// </returns>
        [CanBeNull]
        internal PropertyInfo GetProperty<T>(string name) => _cache.GetOrAdd(typeof(T), CacheType)
            .TryGetValue(name, out var propInfo)
            ? propInfo
            : null;

        /// <summary>
        ///     Selects <see cref="PropertyInfo" />s of type <paramref name="type" />
        /// </summary>
        /// <param name="type">Type to select and cache properties of</param>
        /// <returns>
        ///     A dictionary of <see cref="MemberInfo.Name" /> to <see cref="PropertyInfo" /> of selected properties of type
        ///     <paramref name="type" />
        /// </returns>
        [NotNull]
        private static Dictionary<string, PropertyInfo> CacheType(Type type)
        {
            return type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                               BindingFlags.FlattenHierarchy)
                .ToDictionary(propInfo => propInfo.Name, propInfo => propInfo, StringComparer.OrdinalIgnoreCase);
        }
    }
}