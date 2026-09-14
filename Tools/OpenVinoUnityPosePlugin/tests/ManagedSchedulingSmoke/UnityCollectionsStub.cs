namespace Unity.Collections
{
    public readonly struct NativeArray<T>
    {
        private readonly T[] _data;

        public NativeArray(T[] data)
        {
            _data = data;
        }

        public bool IsCreated => _data != null;
        public int Length => _data?.Length ?? 0;

        public void CopyTo(T[] destination)
        {
            if (_data == null)
            {
                throw new System.InvalidOperationException("NativeArray stub is not created.");
            }
            System.Array.Copy(_data, destination, _data.Length);
        }
    }
}
