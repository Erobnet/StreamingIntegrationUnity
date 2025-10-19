using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Rendering
{
    [MaterialProperty("_DarkerColor")]
    struct DarkerColorVector4Override : IComponentData
    {
        public float4 Value;
    }
}
