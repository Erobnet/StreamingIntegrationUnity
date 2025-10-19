using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Rendering
{
    [MaterialProperty("_SecondaryColor")]
    struct SecondaryColorVector4Override : IComponentData
    {
        public float4 Value;
    }
}
