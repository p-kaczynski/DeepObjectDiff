namespace DeepObjectDiff
{
    public struct ObjectDifference
    {
        public string Path { get; }
        public object First { get; }
        public object Second { get; }

        public ObjectDifference(string path, object first, object second)
        {
            Path = path;
            First = first;
            Second = second;
        }
    }
}