using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Rendering
{
    [MaterialProperty("_AccentColor")]
    struct AccentColorVector4Override : IComponentData
    {
        public float4 Value;
    }
}
