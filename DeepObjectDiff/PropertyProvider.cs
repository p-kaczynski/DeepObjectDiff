using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

namespace DeepObjectDiff
{
    internal class PropertyProvider
    {
        private readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> _cache =
            new ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>>();

        [NotNull]
        internal IEnumerable<PropertyInfo> GetAllProperties<T>() => _cache.GetOrAdd(typeof(T), CacheType).Values;

        [CanBeNull]
        internal PropertyInfo GetProperty<T>(string name)
            => _cache.TryGetValue(typeof(T), out var dict)
               && dict.TryGetValue(name, out var propInfo)
                ? propInfo
                : null;

        [NotNull]
        private static Dictionary<string, PropertyInfo> CacheType(Type type)
        {
            return type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                               BindingFlags.FlattenHierarchy)
                .ToDictionary(propInfo => propInfo.Name, propInfo=>propInfo, StringComparer.OrdinalIgnoreCase);
        }
    }
}