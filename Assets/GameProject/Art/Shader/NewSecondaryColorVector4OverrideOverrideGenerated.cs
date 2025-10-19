using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Rendering
{
    [MaterialProperty("_NewSecondaryColor")]
    struct NewSecondaryColorVector4Override : IComponentData
    {
        public float4 Value;
    }
}
