using System;
using Drboum.Utilities.Runtime;
using Unity.Entities;

namespace GameProject.Persistence
{
    public readonly struct PersistenceInstanceId : IComponentData, IEquatable<PersistenceInstanceId>
    {
        public readonly int Value;

        public PersistenceInstanceId(int value)
        {
            Value = value;
        }

        public PersistenceInstanceId(GuidWrapper value)
        {
            Value = value.GetHashCode();
        }

        public bool Equals(PersistenceInstanceId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is PersistenceInstanceId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public static bool operator ==(PersistenceInstanceId left, PersistenceInstanceId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PersistenceInstanceId left, PersistenceInstanceId right)
        {
            return !left.Equals(right);
        }
    }
}