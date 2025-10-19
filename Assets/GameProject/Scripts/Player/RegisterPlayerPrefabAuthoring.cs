using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GameProject.Player
{
    public class RegisterPlayerPrefabAuthoring : MonoBehaviour
    {
        public LocalPlayerAuthoring SpawnedPlayerPrefab;
        public Transform PlayerSpawnPoint;

        private class Baker : Baker<RegisterPlayerPrefabAuthoring>
        {
            public override void Bake(RegisterPlayerPrefabAuthoring authoring)
            {
                GameProjectHelper.AssertIsPartOfPrefab(authoring, authoring.SpawnedPlayerPrefab.gameObject, nameof(SpawnedPlayerPrefab));

                var prefab = GetEntity(authoring.SpawnedPlayerPrefab, TransformUsageFlags.None);
                Entity entity = GetEntity(authoring, TransformUsageFlags.None);
                AddComponent(entity, new SpawnLocalPlayerPrefab {
                    Value = prefab
                });
                AddComponent(entity, new PlayerSpawnPointData {
                    Value = authoring.PlayerSpawnPoint.position
                });
            }
        }
    }

    [BakingType]
    public struct PlayerSpawnPointData : IComponentData
    {
        public float3 Value;
    }

    [BakingType]
    public struct SpawnLocalPlayerPrefab : IComponentData
    {
        public Entity Value;
    }
}