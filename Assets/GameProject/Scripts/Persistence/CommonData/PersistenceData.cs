using System;
using System.Runtime.CompilerServices;
using Unity.Properties;

namespace GameProject.Persistence.CommonData
{
    public unsafe interface ICustomBinarySerializer<T>
        where T : unmanaged
    {
        public void Serialize(ref SerializationContext context, ReadOnlySpan<T> values);

        public void Deserialize(ref DeserializationContext context, T* writeCollection, int elementLength);
    }

    public static unsafe class CustomBinarySerializerExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SerializeOne<TCustomBinarySerializer, T>(this TCustomBinarySerializer serializer, ref SerializationContext context, T value)
            where TCustomBinarySerializer : ICustomBinarySerializer<T>
            where T : unmanaged
        {
            serializer.Serialize(ref context, new ReadOnlySpan<T>(&value, 1));
        }
    }

    [GeneratePropertyBag]
    public unsafe struct ChatUserPropertiesPersistence
    {
        public GameCurrency Currency;
        public SkinOptions SkinOptions;
        public SkinColorOptions SkinColorOptions;
    }
}