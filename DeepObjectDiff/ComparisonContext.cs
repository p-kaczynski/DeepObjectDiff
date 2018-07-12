using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DeepObjectDiff
{
    internal class ComparisonContext : ICloneable
    {
        private readonly Stack<string> _stack;
        private readonly List<ObjectDifference> _differences;
        private readonly Stack<WeakReference> _firstObjectPath;
        private readonly Stack<WeakReference> _secondObjectPath;
        private readonly HashSet<WeakReference> _firstObjectPathSet;
        private readonly HashSet<WeakReference> _secondObjectPathSet;
        
        public bool PreventDifferenceGeneration { get; set; }


        internal ComparisonContext()
        {
            _stack = new Stack<string>();
            _differences = new List<ObjectDifference>();
            _firstObjectPath = new Stack<WeakReference>();
            _secondObjectPath = new Stack<WeakReference>();
            _firstObjectPathSet = new HashSet<WeakReference>(ReferenceEqualityComparer.Instance);
            _secondObjectPathSet = new HashSet<WeakReference>(ReferenceEqualityComparer.Instance);
        }

        private ComparisonContext(Stack<string> stack, List<ObjectDifference> differences, Stack<WeakReference> firstObjectPath, Stack<WeakReference> secondObjectPath)
        {
            _stack = stack;
            _differences = differences;
            _firstObjectPath = firstObjectPath;
            _secondObjectPath = secondObjectPath;
            _firstObjectPathSet = new HashSet<WeakReference>(firstObjectPath, ReferenceEqualityComparer.Instance);
            _secondObjectPathSet = new HashSet<WeakReference>(secondObjectPath, ReferenceEqualityComparer.Instance);
        }

        internal void Enter(string name)
            => _stack.Push(name);

        internal bool TryEnterObject<TRef>(TRef first, TRef second)
        {
            if (typeof(TRef).IsValueType || typeof(string).IsValueType) return true;

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

        internal void ExitObject<TRef>()
        {
            if (typeof(TRef).IsValueType || typeof(string).IsValueType) return;

            var popped = _firstObjectPath.Pop();
            _firstObjectPathSet.Remove(popped);

            popped = _secondObjectPath.Pop();
            _secondObjectPathSet.Remove(popped);
        }

        internal void Exit()
            => _stack.Pop();

        internal void AddDifference(ObjectDifference difference)
        {
            if (!PreventDifferenceGeneration)
                _differences.Add(difference);
        }

        internal void AddDifferenceAtCurrentPath(object first, object second)
        {
            if(!PreventDifferenceGeneration)
                _differences.Add(new ObjectDifference(GetCurrentPath(), first, second));
        }

        internal string GetCurrentPath() => $"/{string.Join("/", _stack.Reverse())}";

        internal ObjectDifference[] Differences => _differences.ToArray();


        public object Clone()
        {
            // Yes, this on purpose passes a NEW stack and EXISTING list (as we want the differences)
            // and we also create a new object path stack, as in multithreaded scenario each thread will have it's own path it takes
            return new ComparisonContext(new Stack<string>(_stack), _differences, new Stack<WeakReference>(_firstObjectPath), new Stack<WeakReference>(_secondObjectPath));
        }

        private  class ReferenceEqualityComparer : IEqualityComparer<WeakReference>
        {
            public static readonly IEqualityComparer<WeakReference> Instance = new ReferenceEqualityComparer();
            bool IEqualityComparer<WeakReference>.Equals(WeakReference x, WeakReference y) => ReferenceEquals(x?.Target, y?.Target);

            int IEqualityComparer<WeakReference>.GetHashCode(WeakReference obj) => RuntimeHelpers.GetHashCode(obj.Target);
        }
    }
}
