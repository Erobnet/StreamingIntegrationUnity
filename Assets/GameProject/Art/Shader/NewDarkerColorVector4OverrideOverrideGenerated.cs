using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Rendering
{
    [MaterialProperty("_NewDarkerColor")]
    struct NewDarkerColorVector4Override : IComponentData
    {
        public float4 Value;
    }
}
