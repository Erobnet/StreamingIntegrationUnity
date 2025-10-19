using System;
using Unity.Collections;
using PersistData = GameProject.Persistence.CommonData.SkinOptions;
using FixedString = Unity.Collections.FixedString512Bytes;

namespace GameProject.Persistence.CommonData
{
    [Serializable]
    public struct SkinOptions
    {
        public FixedList32Bytes<byte> Indices;

        public FixedString ToFixedString()
        {
            var result = new FixedString();
            result.Append((FixedString32Bytes)nameof(SkinOptions));
            result.Append((FixedString32Bytes)": [");
            for ( var i = 0; i < Indices.Length; i++ )
            {
                byte index = Indices[i];
                result.Append(index);
                if ( i < Indices.Length - 1 )
                    result.Append(',');
            }
            result.Append(']');

            return result;
        }

        public unsafe struct BinaryAdapter : ICustomBinarySerializer<PersistData>
        {
            public void Serialize(ref SerializationContext context, ReadOnlySpan<PersistData> values)
            {
                for ( int i = 0; i < values.Length; i++ )
                {
                    context.Write(values[i].Indices);
                }
            }

            public void Deserialize(ref DeserializationContext context, PersistData* writeCollection, int elementLength)
            {
                for ( int i = 0; i < elementLength; i++ )
                {
                    context.ReadNext(ref writeCollection[i].Indices);
                }
            }
        }
    }
}