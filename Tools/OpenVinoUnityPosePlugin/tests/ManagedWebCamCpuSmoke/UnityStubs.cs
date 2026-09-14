using System;

namespace UnityEngine
{
    public struct Color32
    {
        public Color32(byte r, byte g, byte b, byte a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public byte r;
        public byte g;
        public byte b;
        public byte a;
    }
}

namespace Unity.Collections
{
    public struct NativeArray<T> : IDisposable
    {
        private T[] _items;

        public NativeArray(int length)
        {
            _items = new T[length];
        }

        public bool IsCreated => _items != null;
        public int Length => _items == null ? 0 : _items.Length;

        public T this[int index]
        {
            get => _items[index];
            set => _items[index] = value;
        }

        public void Dispose()
        {
            _items = null;
        }
    }
}
