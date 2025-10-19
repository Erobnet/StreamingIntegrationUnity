using System;
using GameProject.Animation;
using GameProject.Characters;
using GameProject.Persistence.CommonData;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Extensions;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;

namespace GameProject.Player
{
    public class LocalPlayerAuthoring : MonoBehaviour
    {
        [SerializeField] private GameCharacterAuthoring gameCharacterAuthoring;
        [SerializeField] private CharacterCreatorData characterCreatorData;
        [SerializeField] private byte[] skinOptions = Array.Empty<byte>();
        [SerializeField] private byte[] skinColorOptionsForCategory = Array.Empty<byte>();
        [SerializeField] private uint startCurrency;

        private void OnValidate()
        {
            if ( characterCreatorData == null )
                return;

            SyncArray(ref skinOptions, characterCreatorData.Categories);
            SyncArray(ref skinColorOptionsForCategory, characterCreatorData.Categories);

            for ( var index = 0; index < skinOptions.Length; index++ )
            {
                var category = characterCreatorData.Categories[index];
                byte skinOption = skinOptions[index];
                if ( skinOption >= category.OptionSets.Length )
                {
                    var newSkinOption = (byte)math.max(category.OptionSets.Length - 1, 0);
                    if ( newSkinOption != skinOption )
                    {
                        skinOptions[index] = newSkinOption;
                        skinOption = newSkinOption;
                        this.SetDirtySafe();
                    }
                }

                var libraryTransitionData = category.OptionSets[skinOption];
                byte colorOptionValue = skinColorOptionsForCategory[index];
                if ( colorOptionValue >= libraryTransitionData.ColorOptions.Length )
                {
                    if ( libraryTransitionData.ColorOptions.Length == 0 && colorOptionValue != 0 )
                    {
                        LogHelper.LogInfoMessage($"no color option available for index ='{index}' with the skin {libraryTransitionData.DisplayName} in category {category.CategoryDisplayName} at {nameof(skinColorOptionsForCategory)}", $"Validation", this);
                    }
                    var newVal = (byte)math.max(libraryTransitionData.ColorOptions.Length - 1, 0);
                    if ( newVal != colorOptionValue )
                    {
                        skinColorOptionsForCategory[index] = newVal;
                        this.SetDirtySafe();
                    }
                }
            }
        }

        private void SyncArray<T>(ref byte[] validationArray, T[] referenceArray)
        {
            if ( validationArray.Length != referenceArray.Length )
            {
                var validArray = new byte[referenceArray.Length];
                Array.Copy(validationArray, validArray, math.min(referenceArray.Length, validationArray.Length));
                validationArray = validArray;
                if ( validationArray.Length > referenceArray.Length )
                    LogHelper.LogErrorMessage($"{nameof(validationArray)} is out of the range option ({referenceArray.Length}), removed excess", $"Validation", this);
            }
        }

        private class Baker : Baker<LocalPlayerAuthoring>
        {
            public override void Bake(LocalPlayerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.ManualOverride);

                DependsOn(authoring.gameCharacterAuthoring);
                DependsOn(authoring.gameCharacterAuthoring.gameObject);
                AddComponent(entity, new CharacterHierarchyHubData {
                    AnimationRoot = GetEntity(authoring.gameCharacterAuthoring.characterAnimator, TransformUsageFlags.Dynamic),
                    MovementRoot = GetEntity(authoring.gameCharacterAuthoring.characterRoot, TransformUsageFlags.Dynamic)
                });
                var skinOptionApplies = AddBuffer<PersistentSkinOptionApply>(entity);
                foreach ( byte skinIndex in authoring.skinOptions )
                {
                    skinOptionApplies.Add(new() {
                        Value = new() {
                            Index = skinIndex
                        }
                    });
                }
                var skinColorOptionApplies = AddBuffer<PersistentSkinColorOptionApply>(entity);

                foreach ( byte skinColorIndex in authoring.skinColorOptionsForCategory )
                {
                    skinColorOptionApplies.Add(new() {
                        Value = new() {
                            Index = skinColorIndex
                        }
                    });
                }

                this.BakeChatBotUserRootRequiredComponents(entity);
                AddComponent<LocalPlayerTag>(entity);
                AddComponent(entity, new GameCurrency {
                    Value = authoring.startCurrency
                });
            }
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateInGroup(typeof(PostBakingSystemGroup))]
    internal partial struct PlayerBakingSystem : ISystem
    {
        [BurstCompile]
        unsafe void ISystem.OnUpdate(ref SystemState state)
        {
            var bakedPlayerRootQuery = SystemAPI.QueryBuilder()
                .WithAll<LinkedEntityGroup, CharacterHierarchyHubData, LocalPlayerTag, PersistentSkinOptionApply, PersistentSkinColorOptionApply>()
                .WithOptions(EntityQueryOptions.IncludePrefab)
                .Build();
            var localPlayerEntities = new NativeList<Entity>(bakedPlayerRootQuery.CalculateEntityCountWithoutFiltering(), Allocator.TempJob);

            new BakePlayerRootJob {
                PlayerEntities = localPlayerEntities,
                PersistentSkinOptionApplyLookup = SystemAPI.GetBufferLookup<PersistentSkinOptionApply>(),
                PersistentSkinColorOptionApplyLookup = SystemAPI.GetBufferLookup<PersistentSkinColorOptionApply>()
            }.Run(bakedPlayerRootQuery);

            var playerRootEntities = bakedPlayerRootQuery.ToEntityArray(Allocator.Temp);
            state.EntityManager.RemoveComponent<PersistentSkinOptionApply>(playerRootEntities);
            state.EntityManager.RemoveComponent<PersistentSkinColorOptionApply>(playerRootEntities);
            //remove hierarchy from player movement entity as it is now on the player root instead
            var playerMovementRootEntitiesAsArray = localPlayerEntities.AsArray();
            state.EntityManager.RemoveComponent<CharacterHierarchyHubData>(playerMovementRootEntitiesAsArray);
            state.EntityManager.AddComponent<LocalPlayerTag>(localPlayerEntities.AsArray());
            localPlayerEntities.Dispose();

            foreach ( var (playerSpawnPointData, spawnLocalPlayerPrefab)
                     in SystemAPI.Query<PlayerSpawnPointData, SpawnLocalPlayerPrefab>() )
            {
                var hierarchyHubData = state.EntityManager.GetComponentData<CharacterHierarchyHubData>(spawnLocalPlayerPrefab.Value);
                ref var localTransform = ref state.EntityManager.GetComponentDataRW<LocalTransform>(hierarchyHubData.MovementRoot).ValueRW;
                localTransform.Position = playerSpawnPointData.Value;
            }
        }
    }

    [BurstCompile]
    public partial struct BakePlayerRootJob : IJobEntity
    {
        private static readonly ProfilerMarker _ExecuteMarker = new(nameof(BakePlayerRootJob) + "Marker");

        public NativeList<Entity> PlayerEntities;
        [NativeDisableContainerSafetyRestriction] public BufferLookup<PersistentSkinOptionApply> PersistentSkinOptionApplyLookup;
        [NativeDisableContainerSafetyRestriction] public BufferLookup<PersistentSkinColorOptionApply> PersistentSkinColorOptionApplyLookup;

        public void Execute(in CharacterHierarchyHubData hierarchyHub, DynamicBuffer<PersistentSkinOptionApply> skinOptionApplyBuffer, DynamicBuffer<PersistentSkinColorOptionApply> colorOptionApplyBuffer, DynamicBuffer<LinkedEntityGroup> linkedEntityGroup)
        {
            _ExecuteMarker.Begin();
            PlayerEntities.AddRange(linkedEntityGroup.Reinterpret<Entity>().AsNativeArray().GetSubArray(1, linkedEntityGroup.Length - 1));
            var animSkinApply = PersistentSkinOptionApplyLookup[hierarchyHub.AnimationRoot];
            animSkinApply.Clear();
            animSkinApply.AddRange(skinOptionApplyBuffer.AsNativeArray());

            var animSkinColorApply = PersistentSkinColorOptionApplyLookup[hierarchyHub.AnimationRoot];
            animSkinColorApply.Clear();
            animSkinColorApply.AddRange(colorOptionApplyBuffer.AsNativeArray());
            _ExecuteMarker.End();
        }
    }

    public struct LocalPlayerTag : IComponentData
    { }
}