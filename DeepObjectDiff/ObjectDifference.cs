using System;
using JetBrains.Annotations;

namespace DeepObjectDiff
{
    /// <summary>
    /// Denotes a difference between compared objects, showing where (<see cref="Path"/>) in the object graph the difference was found
    /// </summary>
    public struct ObjectDifference
    {
        /// <summary>
        /// Path in the object graph denoting where difference was found, in the format of '/PropertyName/SubPropertyName' (/ is root object passed to <see cref="ObjectComparer.Compare{T}"/>)
        /// </summary>
        [PublicAPI]
        public string Path { get; }

        /// <summary>
        /// An object, or nested object of the first object passed to <see cref="ObjectComparer.Compare{T}"/>
        /// </summary>
        [PublicAPI]
        public object First { get; }

        /// <summary>
        /// An object, or nested object of the second object passed to <see cref="ObjectComparer.Compare{T}"/>, that differs from <see cref="First"/>
        /// </summary>
        [PublicAPI]
        public object Second { get; }

        /// <summary>
        /// Creates a new <see cref="ObjectDifference"/>
        /// </summary>
        /// <param name="path">Path where difference was found (obtain from <see cref="ComparisonContext.GetCurrentPath"/></param>
        /// <param name="first">First object, or child of, passed to <see cref="ObjectComparer.Compare{T}"/></param>
        /// <param name="second">Second object, or child of, passed to <see cref="ObjectComparer.Compare{T}"/> that differs from <paramref name="first"/></param>
        public ObjectDifference(string path, object first, object second)
        {
            Path = path;
            First = first;
            Second = second;
        }
    }
}