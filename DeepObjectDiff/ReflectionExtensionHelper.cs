using System;
using System.Collections.Generic;
using System.Linq;

namespace DeepObjectDiff
{
    internal static class ReflectionExtensionHelper
    {
        internal static bool TryAsGenericDictionary(this Type type, out Type keyType, out Type valueType)
        {
            var theInterface = type.GetInterfaces()
                .SingleOrDefault(t => t.IsGenericType
                                      && t.GetGenericTypeDefinition() == typeof(IDictionary<,>));

            keyType = theInterface?.GetGenericArguments()[0];
            valueType = theInterface?.GetGenericArguments()[1];

            return theInterface != null;
        }
            

        internal static bool TryAsGenericSet(this Type type, out Type elementType)
            => TryAsGeneric(type, typeof(ISet<>), out elementType);

        internal static bool TryAsGenericList(this Type type, out Type elementType)
            => TryAsGeneric(type, typeof(IList<>), out elementType);

        internal static bool TryAsGenericEnumerable(this Type type, out Type elementType) 
            => TryAsGeneric(type, typeof(IEnumerable<>), out elementType);

        private static bool TryAsGeneric(this Type type, Type genericInterface,  out Type elementType)
        {
            var theInterface = type.GetInterfaces()
                .SingleOrDefault(t => t.IsGenericType
                          && t.GetGenericTypeDefinition() == genericInterface);
            
            elementType = theInterface?.GetGenericArguments().Single();
            return theInterface != null;
        }
    }
}