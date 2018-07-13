using System;
using System.Collections.Generic;
using System.Linq;

namespace DeepObjectDiff
{
    /// <summary>
    ///     Conains methods that help in working with reflection
    /// </summary>
    internal static class ReflectionExtensionHelper
    {
        /// <summary>
        ///     Checks if the type <paramref name="type" /> implements <see cref="IDictionary{TKey,TValue}" />, and if so returns
        ///     key and value types
        /// </summary>
        /// <param name="type">Type to check if implements <see cref="IDictionary{TKey,TValue}" /></param>
        /// <param name="keyType">If <paramref name="type" /> is <see cref="IDictionary{TKey,TValue}" />, returns the key type</param>
        /// <param name="valueType">If <paramref name="type" /> is <see cref="IDictionary{TKey,TValue}" />, returns the value type</param>
        /// <returns>
        ///     <c>true</c> if <paramref name="type" /> implements <see cref="IDictionary{TKey,TValue}" />, otherwise
        ///     <c>false</c>
        /// </returns>
        internal static bool TryAsGenericDictionary(this Type type, out Type keyType, out Type valueType)
        {
            var theInterface = type.GetInterfaces()
                .SingleOrDefault(t => t.IsGenericType
                                      && t.GetGenericTypeDefinition() == typeof(IDictionary<,>));

            keyType = theInterface?.GetGenericArguments()[0];
            valueType = theInterface?.GetGenericArguments()[1];

            return theInterface != null;
        }

        /// <summary>
        ///     Checks if the type <paramref name="type" /> implements <see cref="ISet{T}" />, and if so returns element type
        /// </summary>
        /// <param name="type">Type to check if implements <see cref="ISet{T}" /></param>
        /// <param name="elementType">If <paramref name="type" /> is <see cref="ISet{T}" /> returns element type</param>
        /// <returns><c>true</c> if <paramref name="type" /> implements <see cref="ISet{T}" />, otherwise <c>false</c></returns>
        internal static bool TryAsGenericSet(this Type type, out Type elementType) =>
            TryAsGeneric(type, typeof(ISet<>), out elementType);

        /// <summary>
        ///     Checks if the type <paramref name="type" /> implements <see cref="IList{T}" />, and if so returns element type
        /// </summary>
        /// <param name="type">Type to check if implements <see cref="IList{T}" /></param>
        /// <param name="elementType">If <paramref name="type" /> is <see cref="IList{T}" />, returns the element type</param>
        /// <returns><c>true</c> if <paramref name="type" /> implements <see cref="IList{T}" /></returns>
        internal static bool TryAsGenericList(this Type type, out Type elementType) =>
            TryAsGeneric(type, typeof(IList<>), out elementType);

        /// <summary>
        ///     CHecks if the type <paramref name="type" /> implements <see cref="IEnumerable{T}" />, and if so returns element
        ///     type
        /// </summary>
        /// <param name="type">Type to check if implements <see cref="IEnumerable{T}" /></param>
        /// <param name="elementType">If <paramref name="type" /> implements <see cref="IEnumerable{T}" />, returns element type</param>
        /// <returns><c>true</c> if <paramref name="type" /> implements <see cref="IEnumerable{T}" /></returns>
        internal static bool TryAsGenericEnumerable(this Type type, out Type elementType) =>
            TryAsGeneric(type, typeof(IEnumerable<>), out elementType);

        /// <summary>
        ///     Helper to check if <paramref name="type" /> implements <paramref name="genericInterface" /> and if so, returns its
        ///     <paramref name="elementType" />
        /// </summary>
        /// <param name="type">Type to check if implements <paramref name="genericInterface" /></param>
        /// <param name="genericInterface"></param>
        /// <param name="elementType">
        ///     If <see cref="type" /> implements <paramref name="genericInterface" /> returns its element
        ///     type (i.e. its first generic argument)
        /// </param>
        /// <returns>
        ///     <c>true</c> if <paramref name="type" /> implements <paramref name="genericInterface" />, otherwise
        ///     <c>false</c>
        /// </returns>
        /// <remarks>
        ///     This is not fool-proof, but it is a private method used only here, so noo need to worry about misuse
        /// </remarks>
        private static bool TryAsGeneric(this Type type, Type genericInterface, out Type elementType)
        {
            var theInterface = type.GetInterfaces()
                .SingleOrDefault(t => t.IsGenericType
                                      && t.GetGenericTypeDefinition() == genericInterface);

            elementType = theInterface?.GetGenericArguments().Single();
            return theInterface != null;
        }
    }
}