using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DeepObjectDiff
{
    /// <summary>
    /// This object represents a context that is passed down during traversal of the object graph to keep track of things like where in the graph we are,
    /// which objects were visited and so on. It is clonable, because at some points during object graph traversal we need to ensure that we don't mess up the stacks
    /// especially in a multithreaded scenario
    /// </summary>
    internal class ComparisonContext : ICloneable
    {
        private readonly Stack<string> _stack;
        private readonly List<ObjectDifference> _differences;
        private readonly Stack<WeakReference> _firstObjectPath;
        private readonly Stack<WeakReference> _secondObjectPath;
        private readonly HashSet<WeakReference> _firstObjectPathSet;
        private readonly HashSet<WeakReference> _secondObjectPathSet;
        
        /// <summary>
        /// Allows preventing generating differences. Used when <see cref="ObjectComparer.Compare{T}"/> is used to compare elements of a collection (it would generate
        /// a massive amount of differences when looking for "equal" objects in an unordered collection)
        /// </summary>
        internal bool PreventDifferenceGeneration { get; set; }

        /// <summary>
        /// Initialises a new instance of <see cref="ComparisonContext"/>
        /// </summary>
        internal ComparisonContext()
        {
            _stack = new Stack<string>();
            _differences = new List<ObjectDifference>();
            _firstObjectPath = new Stack<WeakReference>();
            _secondObjectPath = new Stack<WeakReference>();
            _firstObjectPathSet = new HashSet<WeakReference>(ReferenceEqualityComparer.Instance);
            _secondObjectPathSet = new HashSet<WeakReference>(ReferenceEqualityComparer.Instance);
        }

        /// <summary>
        /// Used only by <see cref="Clone"/> method
        /// </summary>
        private ComparisonContext(Stack<string> stack, List<ObjectDifference> differences, Stack<WeakReference> firstObjectPath, Stack<WeakReference> secondObjectPath)
        {
            _stack = stack;
            _differences = differences;
            _firstObjectPath = firstObjectPath;
            _secondObjectPath = secondObjectPath;
            _firstObjectPathSet = new HashSet<WeakReference>(firstObjectPath, ReferenceEqualityComparer.Instance);
            _secondObjectPathSet = new HashSet<WeakReference>(secondObjectPath, ReferenceEqualityComparer.Instance);
        }

        /// <summary>
        /// Remembers a name of an object graph node the comparison has entered, as inside the object it is impossible to find out the name of field/property whence we've came 
        /// </summary>
        /// <param name="name"></param>
        internal void Enter(string name)
            => _stack.Push(name);

        /// <summary>
        /// Tries to note entering a particular reference-type object.
        /// It will return false, if in current graph traverse path we have alredy entered this particular object, which means there is a circular reference, and code would never finish
        /// </summary>
        /// <typeparam name="TRef">Type of the object entered</typeparam>
        /// <param name="first">First object of comparison</param>
        /// <param name="second">Second object of comparison</param>
        /// <returns><c>true</c> if the object is a <see cref="string"/>, a <see cref="ValueType"/> or if this is the first time in current recursion path the object has been entered, otherwise <c>false</c></returns>
        /// <remarks>
        /// This uses <see cref="WeakReference"/> to be extra-sure we won't cause GC to prevent collection of compared objects, if by some means this context persists longer than the compared objects.
        /// </remarks>
        internal bool TryEnterObject<TRef>(TRef first, TRef second)
        {
            if (typeof(TRef).IsValueType || typeof(TRef) == typeof(string)) return true;

            var firstWr = new WeakReference(first);
            var secondWr = new WeakReference(second);

            if (!_firstObjectPathSet.Add(firstWr)) return false;
            if (!_secondObjectPathSet.Add(secondWr))
            {
                _firstObjectPathSet.Remove(firstWr);
                return false;
            }

            _firstObjectPath.Push(firstWr);
            _secondObjectPath.Push(secondWr);
            return true;
        }

        /// <summary>
        /// Informs the context that an object comparison has been finished, and <see cref="ObjectComparer.Compare{T}"/> will return.
        /// This removes information about having entered last object and will not cause <c>false</c> return from <see cref="TryEnterObject{TRef}"/> if the object is entered again in a different execution path
        /// </summary>
        /// <typeparam name="TRef"></typeparam>
        internal void ExitObject<TRef>()
        {
            if (typeof(TRef).IsValueType || typeof(string).IsValueType) return;

            var popped = _firstObjectPath.Pop();
            _firstObjectPathSet.Remove(popped);

            popped = _secondObjectPath.Pop();
            _secondObjectPathSet.Remove(popped);
        }

        /// <summary>
        /// Forgets the (field/property) name of last entered object, as the differences found from now on will not be in that particular object
        /// </summary>
        internal void Exit()
            => _stack.Pop();

        /// <summary>
        /// Adds a pre-made <see cref="ObjectDifference"/> to the difference collection
        /// </summary>
        /// <param name="difference">A difference between compared objects</param>
        internal void AddDifference(ObjectDifference difference)
        {
            if (!PreventDifferenceGeneration)
                _differences.Add(difference);
        }

        /// <summary>
        /// Requests adding an <see cref="ObjectDifference"/> using current path.
        /// </summary>
        /// <param name="first">First object</param>
        /// <param name="second">A second object that in some way differs from <paramref name="first"/></param>
        internal void AddDifferenceAtCurrentPath(object first, object second)
        {
            if(!PreventDifferenceGeneration)
                _differences.Add(new ObjectDifference(GetCurrentPath(), first,second));
        }

        /// <summary>
        /// Generates a string denoting current path in the object graph, like '/PropertyName/SubPropertyName`
        /// </summary>
        internal string GetCurrentPath() => $"/{string.Join("/", _stack.Reverse())}";

        /// <summary>
        /// Returns an array with differences recorded during comparison
        /// </summary>
        internal ObjectDifference[] Differences => _differences.ToArray();

        /// <summary>
        /// Clones the <see cref="ComparisonContext"/> in a predefined way to allow for independent traversal of different paths of the object graph
        /// </summary>
        /// <returns></returns>
        public object Clone()
        {
            // Yes, this on purpose passes a NEW stack and EXISTING list (as we want the differences)
            // and we also create a new object path stack, as in multithreaded scenario each thread will have it's own path it takes
            return new ComparisonContext(new Stack<string>(_stack), _differences, new Stack<WeakReference>(_firstObjectPath), new Stack<WeakReference>(_secondObjectPath));
        }

        /// <inheritdoc />
        /// <summary>
        /// A simple comparer of <see cref="T:System.WeakReference" />s to determine whether two WeakReferences are pointing to the same object instance
        /// </summary>
        private  class ReferenceEqualityComparer : IEqualityComparer<WeakReference>
        {
            /// <summary>
            /// Provides a single instance of the <see cref="ReferenceEqualityComparer"/> to be reused. There is no need for it to be created more than once
            /// </summary>
            internal static readonly IEqualityComparer<WeakReference> Instance = new ReferenceEqualityComparer();
            
            /// <summary>
            /// Private constructor prevents creation of additional copies of this comparer outside of this class
            /// </summary>
            private ReferenceEqualityComparer() { }

            bool IEqualityComparer<WeakReference>.Equals(WeakReference x, WeakReference y) => ReferenceEquals(x?.Target, y?.Target);

            int IEqualityComparer<WeakReference>.GetHashCode(WeakReference obj) => RuntimeHelpers.GetHashCode(obj.Target);
        }
    }
}
