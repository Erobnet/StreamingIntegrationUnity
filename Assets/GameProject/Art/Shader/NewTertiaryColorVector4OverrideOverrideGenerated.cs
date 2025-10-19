using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Rendering
{
    [MaterialProperty("_NewTertiaryColor")]
    struct NewTertiaryColorVector4Override : IComponentData
    {
        public float4 Value;
    }
}
