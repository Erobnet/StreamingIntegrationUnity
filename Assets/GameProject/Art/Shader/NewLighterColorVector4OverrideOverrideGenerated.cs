using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Rendering
{
    [MaterialProperty("_NewLighterColor")]
    struct NewLighterColorVector4Override : IComponentData
    {
        public float4 Value;
    }
}
