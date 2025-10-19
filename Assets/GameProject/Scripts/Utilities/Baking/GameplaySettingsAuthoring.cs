using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GameProject
{
    public class GameplaySettingsAuthoring : MonoBehaviour
    {
        public Vector3 OffsetFromPlayerWithMirrorCamera;

        private class Baker : Baker<GameplaySettingsAuthoring>
        {
            public override void Bake(GameplaySettingsAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.None);
                AddComponent(entity, new GameplaySettingsData {
                    OffsetFromPlayerWithMirrorCamera = authoring.OffsetFromPlayerWithMirrorCamera,
                });
            }
        }
    }

    public struct GameplaySettingsData : IComponentData
    {
        public float3 OffsetFromPlayerWithMirrorCamera;
    }
}