using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Properties;

namespace GameProject.Persistence.CommonData
{
    [GeneratePropertyBag]
    [Serializable]
    public struct GameCurrency : IComponentData, IEquatable<GameCurrency>
    {
        public uint Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(GameCurrency other)
        {
            return Value == other.Value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            return obj is GameCurrency other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator GameCurrency(uint value)
        {
            return new GameCurrency {
                Value = value
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(GameCurrency left, GameCurrency right)
        {
            return left.Equals(right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(GameCurrency left, GameCurrency right)
        {
            return !left.Equals(right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(GameCurrency left, GameCurrency right)
        {
            return left.Value < right.Value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(GameCurrency left, GameCurrency right)
        {
            return left.Value > right.Value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <=(GameCurrency left, GameCurrency right)
        {
            return left.Value <= right.Value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >=(GameCurrency left, GameCurrency right)
        {
            return left.Value >= right.Value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GameCurrency operator -(GameCurrency left, GameCurrency right)
        {
            return (left.Value - right.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GameCurrency operator +(GameCurrency left, GameCurrency right)
        {
            return (left.Value + right.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GameCurrency operator *(GameCurrency left, GameCurrency right)
        {
            return (left.Value * right.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GameCurrency operator /(GameCurrency left, GameCurrency right)
        {
            return (left.Value / right.Value);
        }

        public unsafe struct BinaryAdapter : ICustomBinarySerializer<GameCurrency>
        {
            public void Serialize(ref SerializationContext context, ReadOnlySpan<GameCurrency> values)
            {
                context.Write(values);
            }

            public void Deserialize(ref DeserializationContext context, GameCurrency* writeCollection, int elementLength)
            {
                UnsafeUtility.MemCpy(writeCollection, context.ReadNext<GameCurrency>(elementLength), elementLength * sizeof(GameCurrency));
            }
        }
    }
}