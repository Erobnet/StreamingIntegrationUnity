using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Rendering
{
    [MaterialProperty("_LighterColor")]
    struct LighterColorVector4Override : IComponentData
    {
        public float4 Value;
    }
}
