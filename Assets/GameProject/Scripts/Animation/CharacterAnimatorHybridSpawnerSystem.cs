using ChatBot.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Hybrid;
using UnityEngine;

namespace GameProject.Animation
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(ChatBotSystemGroup))] //spawns the characters
    internal partial struct CharacterAnimatorHybridSpawnerSystem : ISystem
    {
        void ISystem.OnUpdate(ref SystemState state)
        {
            EntityQuery unintializedSpawnPrefabQuery = SystemAPI.QueryBuilder()
                .WithAll<CharacterAnimatorPrefab>()
                .WithNone<CharacterAnimatorSpawnInstance>()
                .Build();

            NativeArray<Entity> unintializedSpawnPrefabEntities = DoStructuralChangeOnSpawnPrefabEntities(ref state, unintializedSpawnPrefabQuery);

            for ( var index = 0; index < unintializedSpawnPrefabEntities.Length; index++ )
            {
                var spawnPrefabEntity = unintializedSpawnPrefabEntities[index];
                var spawnPrefabComponent = state.EntityManager.GetComponentData<CharacterAnimatorPrefab>(spawnPrefabEntity);
                var characterAnimator = Object.Instantiate(spawnPrefabComponent.PrefabReference.Value.Component);

                state.EntityManager.SetComponentData(spawnPrefabEntity, new CharacterAnimatorSpawnInstance {
                    Value = characterAnimator,
                    CachedTransform = characterAnimator.transform
                });
                state.EntityManager.SetCompanionLinkComponent(spawnPrefabEntity, characterAnimator.gameObject);
            }
        }

        [BurstCompile]
        private static NativeArray<Entity> DoStructuralChangeOnSpawnPrefabEntities(ref SystemState state, EntityQuery unintializedSpawnPrefabQuery)
        {
            var unintializedSpawnPrefabEntities = unintializedSpawnPrefabQuery.ToEntityArray(Allocator.Temp);
            var componentTypesToAdd = new FixedList128Bytes<ComponentType> {
                ComponentType.ReadWrite<CharacterAnimatorSpawnInstance>(),
                state.EntityManager.GetCompanionLinkComponent()
            };
            var requiredComponents = new ComponentTypeSet(
                componentTypesToAdd
            );
            state.EntityManager.AddComponent(unintializedSpawnPrefabQuery, requiredComponents);
            return unintializedSpawnPrefabEntities;
        }
    }
}