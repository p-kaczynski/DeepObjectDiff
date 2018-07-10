using System;
using System.Collections;
using System.Collections.Generic;

namespace DeepObjectDiff
{
    internal class ComparisonContext : ICloneable
    {
        private readonly Stack<string> _stack;

        internal ComparisonContext()
        {
            _stack = new Stack<string>();
        }

        private ComparisonContext(Stack<string> stack)
        {
            _stack = stack;
        }


        internal void Enter(string name)
            => _stack.Push(name);

        internal void Exit()
            => _stack.Pop();

        internal string GetCurrentPath() => $"/{string.Join("/", _stack)}";


        public object Clone()
        {
            return new ComparisonContext(new Stack<string>(_stack));
        }
    }
}