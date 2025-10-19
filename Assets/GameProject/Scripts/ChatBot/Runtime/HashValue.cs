using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace ChatBot.Runtime
{
    [BurstCompile]
    public readonly struct HashValue : IEquatable<HashValue>
    {
        public readonly uint Value;

        public HashValue(uint value)
        {
            Value = value;
        }

        public static unsafe HashValue Create<TFixedString>(TFixedString command)
            where TFixedString : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            return new HashValue(ToHash(command.GetUnsafePtr(), command.Length));
        }
        
        public static unsafe HashValue Create(string command)
        {
            return Create(command.AsSpan());
        }

        public static unsafe HashValue Create(ReadOnlySpan<char> command)
        {
            fixed ( char* chars = command )
            {
                return new HashValue(ToHash(chars, command.Length));
            }
        }

        public static unsafe HashValue Create(void* data, int byteLength)
        {
            return new HashValue(ToHash(data, byteLength));
        }

        [BurstCompile]
        private static unsafe uint ToHash(void* ptr, int byteLength)
        {
            return CollectionHelper.Hash(ptr, byteLength);
        }

        /// <summary>
        /// Returns a (non-cryptographic) hash of a character buffer (string).
        /// </summary>
        /// <remarks>The hash function used is [djb2](http://web.archive.org/web/20190508211657/http://www.cse.yorku.ca/~oz/hash.html).</remarks>
        /// <param name="ptr">A buffer.</param>
        /// <param name="count">The number of character to hash.</param>
        /// <returns>A hash of the string.</returns>
        private static unsafe uint ToHash(char* ptr, int count)
        {
            // djb2 - Dan Bernstein hash function
            // http://web.archive.org/web/20190508211657/http://www.cse.yorku.ca/~oz/hash.html
            ulong hash = 5381;
            while ( count > 0 )
            {
                ulong c = ptr[--count];
                hash = ((hash << 5) + hash) + c;
            }
            return (uint)hash;
        }

        public bool Equals(HashValue other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is HashValue other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)Value;
        }

        public static bool operator ==(HashValue left, HashValue right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HashValue left, HashValue right)
        {
            return !left.Equals(right);
        }
    }
}