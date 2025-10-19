using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace GameProject.Persistence.CommonData
{
    public unsafe struct SerializationContext
    {
        private NativeList<byte> _buffer;

        public SerializationContext(NativeList<byte> buffer)
        {
            _buffer = buffer;
        }

        public int Length => _buffer.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(T value)
            where T : unmanaged
        {
            WriteRawPtr(&value, sizeof(T));
        }

        /// <summary>
        /// Write the length of an array first then its elements if it is not empty
        /// </summary>
        /// <param name="ptr"></param>
        /// <param name="length"></param>
        /// <typeparam name="TLength">must be an integer number type smaller or equals than (u)int for example: ((s)byte, (u)short, (u)int)</typeparam>
        /// <typeparam name="TElement"></typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteAsArray<TLength, TElement>(TElement* ptr, TLength length)
            where TElement : unmanaged
            where TLength : unmanaged
        {
            Write(length);
            int lengthAsInt = length.GetHashCode();
            WriteRawPtr(ptr, sizeof(TElement) * lengthAsInt);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(NativeArray<T> array)
            where T : unmanaged
        {
            Write(array, array.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(NativeArray<T> array, int count)
            where T : unmanaged
        {
            Write(count);
            WriteRawPtr(array.GetUnsafeReadOnlyPtr(), count * sizeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(T* ptr, int count)
            where T : unmanaged
        {
            WriteRawPtr(ptr, count * sizeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(ReadOnlySpan<T> src)
            where T : unmanaged
        {
            int bufferPos = _buffer.Length;
            _buffer.Length += (src.Length * sizeof(T));
            src.CopyTo(new Span<T>(((byte*)(_buffer.GetUnsafePtr())) + bufferPos, src.Length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteRawPtr(void* ptr, int lengthInBytes)
        {
            _buffer.AddRange(ptr, lengthInBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<TElement>(FixedList32Bytes<TElement> fixedList)
            where TElement : unmanaged
        {
            WriteFixedListWithByteLength<FixedList32Bytes<TElement>, TElement>(ref this, ref fixedList);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<TElement>(FixedList64Bytes<TElement> fixedList)
            where TElement : unmanaged
        {
            WriteFixedListWithByteLength<FixedList64Bytes<TElement>, TElement>(ref this, ref fixedList);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<TElement>(FixedList128Bytes<TElement> fixedList)
            where TElement : unmanaged
        {
            WriteFixedListWithByteLength<FixedList128Bytes<TElement>, TElement>(ref this, ref fixedList);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<TElement>(ref FixedList512Bytes<TElement> fixedList)
            where TElement : unmanaged
        {
            Write<FixedList512Bytes<TElement>, TElement>(ref this, ref fixedList);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<TElement>(ref FixedList4096Bytes<TElement> fixedList)
            where TElement : unmanaged
        {
            Write<FixedList4096Bytes<TElement>, TElement>(ref this, ref fixedList);
        }

        /// <summary>
        /// write the length of the list as a byte instead of ushort effectively reducing its size at the cost of reducing the possible number of element 
        /// </summary>
        /// <param name="fixedList"></param>
        /// <typeparam name="TElement"></typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteFixedListWithByteLength<TElement>(FixedList512Bytes<TElement> fixedList)
            where TElement : unmanaged
        {
            WriteFixedListWithByteLength<FixedList512Bytes<TElement>, TElement>(ref this, ref fixedList);
        }

        /// <inheritdoc cref="WriteFixedListWithByteLength{TElement}(ref Unity.Collections.FixedList512Bytes{TElement})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteFixedListWithByteLength<TElement>(FixedList4096Bytes<TElement> fixedList)
            where TElement : unmanaged
        {
            WriteFixedListWithByteLength<FixedList4096Bytes<TElement>, TElement>(ref this, ref fixedList);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Write<TNativeList, TElement>(ref SerializationContext context, ref TNativeList valueSkinIndices)
            where TElement : unmanaged
            where TNativeList : unmanaged, INativeList<TElement>
        {
            context.WriteRawPtr(UnsafeUtility.AddressOf(ref valueSkinIndices), (valueSkinIndices.Length * UnsafeUtility.SizeOf<TElement>()) + 2); //+2 coz of the length in the front which is 2 byte long
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteFixedListWithByteLength<TNativeList, TElement>(ref SerializationContext context, ref TNativeList valueSkinIndices)
            where TElement : unmanaged
            where TNativeList : unmanaged, INativeList<TElement>
        {
            const int sizeOfLength = sizeof(short);
            byte* addressOfContent = ((byte*)UnsafeUtility.AddressOf(ref valueSkinIndices)) + sizeOfLength;
            byte length = (byte)valueSkinIndices.Length;
            context.Write(length);
            context.WriteRawPtr(addressOfContent, (length * UnsafeUtility.SizeOf<TElement>()));
        }
    }
}