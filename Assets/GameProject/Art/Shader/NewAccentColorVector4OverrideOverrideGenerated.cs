using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Rendering
{
    [MaterialProperty("_NewAccentColor")]
    struct NewAccentColorVector4Override : IComponentData
    {
        public float4 Value;
    }
}
