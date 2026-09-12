#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GoldenNeedle.EditorTools
{
    public static partial class GpuInferenceSpikeSetup
    {
        sealed class FlatBufferReader
        {
            readonly byte[] _data;

            public FlatBufferReader(byte[] data)
            {
                _data = data ?? throw new ArgumentNullException(nameof(data));
                if (_data.Length < 8)
                    throw new InvalidDataException("TFLite flatbuffer is too small.");
            }

            public int RootTable()
            {
                var root = checked((int)ReadUInt32(0));
                Require(root, 4);
                return root;
            }

            public int TableOffset(int table, int fieldIndex)
            {
                Require(table, 4);
                var vtableDistance = ReadInt32(table);
                var vtable = table - vtableDistance;
                Require(vtable, 4);
                var vtableLength = ReadUInt16(vtable);
                var entry = vtable + 4 + (fieldIndex * 2);
                if (entry + 2 > vtable + vtableLength)
                    return 0;
                return ReadUInt16(entry);
            }

            public int Table(int table, int fieldIndex)
            {
                var offset = TableOffset(table, fieldIndex);
                if (offset == 0)
                    return 0;
                var location = table + offset;
                return location + checked((int)ReadUInt32(location));
            }

            public VectorRef Vector(int table, int fieldIndex)
            {
                var offset = TableOffset(table, fieldIndex);
                if (offset == 0)
                    return default;

                var location = table + offset;
                var vector = location + checked((int)ReadUInt32(location));
                var count = ReadInt32(vector);
                if (count < 0)
                    throw new InvalidDataException("Negative FlatBuffer vector length.");
                Require(vector + 4, count == 0 ? 0 : 1);
                return new VectorRef(vector + 4, count);
            }

            public int VectorTable(VectorRef vector, int index)
            {
                CheckVectorIndex(vector, index);
                var element = vector.Data + (index * 4);
                Require(element, 4);
                return element + checked((int)ReadUInt32(element));
            }

            public int VectorInt(VectorRef vector, int index)
            {
                CheckVectorIndex(vector, index);
                return ReadInt32(vector.Data + (index * 4));
            }

            public ushort VectorUInt16(VectorRef vector, int index)
            {
                CheckVectorIndex(vector, index);
                return ReadUInt16(vector.Data + (index * 2));
            }

            public byte VectorByte(VectorRef vector, int index)
            {
                CheckVectorIndex(vector, index);
                return ReadByte(vector.Data + index);
            }

            public byte Byte(int table, int fieldIndex, byte defaultValue)
            {
                var offset = TableOffset(table, fieldIndex);
                return offset == 0 ? defaultValue : ReadByte(table + offset);
            }

            public int Int(int table, int fieldIndex, int defaultValue)
            {
                var offset = TableOffset(table, fieldIndex);
                return offset == 0 ? defaultValue : ReadInt32(table + offset);
            }

            public uint UInt(int table, int fieldIndex, uint defaultValue)
            {
                var offset = TableOffset(table, fieldIndex);
                return offset == 0 ? defaultValue : ReadUInt32(table + offset);
            }

            public string String(int table, int fieldIndex)
            {
                var offset = TableOffset(table, fieldIndex);
                if (offset == 0)
                    return null;

                var location = table + offset;
                var target = location + checked((int)ReadUInt32(location));
                var length = ReadInt32(target);
                if (length < 0)
                    throw new InvalidDataException("Negative FlatBuffer string length.");
                Require(target + 4, length);
                return Encoding.UTF8.GetString(_data, target + 4, length);
            }

            byte ReadByte(int offset)
            {
                Require(offset, 1);
                return _data[offset];
            }

            ushort ReadUInt16(int offset)
            {
                Require(offset, 2);
                return BitConverter.ToUInt16(_data, offset);
            }

            int ReadInt32(int offset)
            {
                Require(offset, 4);
                return BitConverter.ToInt32(_data, offset);
            }

            uint ReadUInt32(int offset)
            {
                Require(offset, 4);
                return BitConverter.ToUInt32(_data, offset);
            }

            void CheckVectorIndex(VectorRef vector, int index)
            {
                if (index < 0 || index >= vector.Count)
                    throw new ArgumentOutOfRangeException(nameof(index));
            }

            void Require(int offset, int count)
            {
                if (offset < 0 || count < 0 || offset > _data.Length - count)
                    throw new InvalidDataException("FlatBuffer offset is outside the model bytes.");
            }

            public readonly struct VectorRef
            {
                public readonly int Data;
                public readonly int Count;

                public VectorRef(int data, int count)
                {
                    Data = data;
                    Count = count;
                }
            }
        }
    }
}
#endif
