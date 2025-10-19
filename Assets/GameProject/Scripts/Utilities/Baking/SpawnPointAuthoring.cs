using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GameProject
{
    public class SpawnPointAuthoring : MonoBehaviour
    {
        public Transform SpawnPoint;

        private class Baker : Baker<SpawnPointAuthoring>
        {
            public override void Bake(SpawnPointAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.None);
                AddComponent(entity,new SpawnPoint {
                    Position = authoring.transform.position,
                    SpawnRotation = authoring.SpawnPoint.transform.rotation,
                });
            }
        }
    }

    public struct SpawnPoint : IComponentData
    {
        public float3 Position;
        public quaternion SpawnRotation;
    }
}