using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Rendering
{
    [MaterialProperty("_TertiaryColor")]
    struct TertiaryColorVector4Override : IComponentData
    {
        public float4 Value;
    }
}
