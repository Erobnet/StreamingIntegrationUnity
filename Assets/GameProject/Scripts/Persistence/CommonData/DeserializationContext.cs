using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace GameProject.Persistence.CommonData
{
    public unsafe struct DeserializationContext
    {
        private const int _SIZE_OF_LENGTH = sizeof(ushort);
        private readonly byte* _buffer;
        private readonly int _length;
        private int _position;

        public DeserializationContext(byte* buffer, int length)
        {
            _buffer = buffer;
            _position = 0;
            _length = length;
        }

        public int Length => _length;
        public bool IsEmpty => _length == 0;

        public int Position {
            get => _position;
            set => _position = math.clamp(value, 0, _length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void* ReadNext<T>(int count)
            where T : unmanaged
        {
            return ReadNext(count * sizeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadNext<T>()
            where T : unmanaged
        {
            return *(T*)ReadNext(sizeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T PeekNext<T>()
            where T : unmanaged
        {
            return *(T*)GetPtrAtPosition(sizeof(T), out _);
        }

        public void ReadNext<T>(out T value)
            where T : unmanaged
        {
            value = *(T*)ReadNext(sizeof(T));
        }

        public NativeArray<TElement> ReadNextArray<TElement>(AllocatorManager.AllocatorHandle allocator, int length)
            where TElement : unmanaged
        {
            var array = CollectionHelper.CreateNativeArray<TElement>(length, allocator, NativeArrayOptions.UninitializedMemory);
            int usersBytesLength = length * sizeof(TElement);
            var bufferSrcPtr = ReadNext(usersBytesLength);
            UnsafeUtility.MemCpy(array.GetUnsafePtr(), bufferSrcPtr, usersBytesLength);
            return array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void* ReadNext(int bytes)
        {
            byte* ptrAtPosition = GetPtrAtPosition(bytes, out int nextPos);
            Position = nextPos;
            return ptrAtPosition;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte* GetPtrAtPosition(int bytes, out int nextPos)
        {
            var ptrAtPosition = _buffer + Position;
            nextPos = Position + bytes;
#if UNITY_EDITOR
            if ( nextPos > _length )
            {
                Debug.LogError($"The position {nextPos} points to memory outside of length {_length}.");
            }
#endif
            return ptrAtPosition;
        }

        public void ReadNext<TElement>(ref FixedList32Bytes<TElement> list)
            where TElement : unmanaged
        {
            var length = ReadNext<byte>();
            ReadNext(ref this, ref list, length);
        }

        public void ReadNext<TElement>(ref FixedList64Bytes<TElement> list)
            where TElement : unmanaged
        {
            var length = ReadNext<byte>();
            ReadNext(ref this, ref list, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ReadNext<TElement>(ref DeserializationContext context, ref FixedList32Bytes<TElement> list, ushort length)
            where TElement : unmanaged
        {
            list.Length = length;
            CopyElements<FixedList32Bytes<TElement>, TElement>(ref context, ref list, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ReadNext<TElement>(ref DeserializationContext context, ref FixedList64Bytes<TElement> list, ushort length)
            where TElement : unmanaged
        {
            list.Length = length;
            CopyElements<FixedList64Bytes<TElement>, TElement>(ref context, ref list, length);
        }

        private static void CopyElements<TNativeList, TElement>(ref DeserializationContext context, ref TNativeList list, ushort length)
            where TElement : unmanaged
            where TNativeList : unmanaged, INativeList<TElement>
        {
            UnsafeUtility.MemCpy((byte*)UnsafeUtility.AddressOf(ref list) + _SIZE_OF_LENGTH, context.ReadNext<TElement>(length), length * sizeof(TElement));
        }
    }
}