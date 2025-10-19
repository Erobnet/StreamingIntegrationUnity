using System.Runtime.CompilerServices;
using GameProject.Persistence;
using GameProject.Persistence.CommonData;
using GameProject.Player;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Extensions;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace GameProject.WorldObjectPlacement
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ObjectPlacementSystem : ISystem, ISystemStartStop
    {
        private static readonly Color _DefaultRendererColor = Color.white;
        private EntityQuery _placementInputDataChangedQuery;
        private PlacementSettingsData _placementSettingsData;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlaceableObjectIndexerLookupSystem.PlaceableObjectIndexerLookup>();
            state.RequireForUpdate<PlacementSettingsData>();
            _placementInputDataChangedQuery = SystemAPI.QueryBuilder()
                .WithPresent<BuildInputData>()
                .Build();
            state.RequireForUpdate(_placementInputDataChangedQuery);
        }

        [BurstCompile]
        public void OnStartRunning(ref SystemState state)
        {
            _placementSettingsData = SystemAPI.GetSingleton<PlacementSettingsData>();
        }

        [BurstCompile]
        void ISystem.OnUpdate(ref SystemState state)
        {
            Entity buildInputEntity = GetBuildInputEntity();
            var buildInputData = state.EntityManager.GetComponentData<BuildInputData>(buildInputEntity);

            CleanUpInstance(ref state, ref buildInputData.DestroyEntityRequest);
            bool backupInstanceExists = HasBackupInstance(ref state, in buildInputData);
#if UNITY_EDITOR
            _placementSettingsData = SystemAPI.GetSingleton<PlacementSettingsData>();
#endif

            if ( !state.EntityManager.IsComponentEnabled<BuildInputData>(buildInputEntity) )
            {
                RevertAndResetBuildInputData(ref state, backupInstanceExists, ref buildInputData, buildInputEntity);
                ApplyBuildInputChanges(ref state, buildInputEntity, in buildInputData);
                return;
            }

            EntityQuery localPlayerQuery = SystemAPI.QueryBuilder()
                .WithAll<GameCurrency, LocalPlayerTag>()
                .Build();

            if ( buildInputData.RotateBuildPerformed
                 && buildInputData.SelectedEntity != Entity.Null )
            {
                if ( buildInputData.IsInMoveMode )
                {
                    var lookup = SystemAPI.GetSingleton<PlaceableObjectIndexerLookupSystem.PlaceableObjectIndexerLookup>();

                    var indexerReference = lookup.Value[state.EntityManager.GetComponentData<PrefabAssetID>(buildInputData.SelectedEntity)];
                    int currentPrefabIndex = indexerReference.IndexInList;
                    var placeableObjectIndices = state.EntityManager.GetBuffer<PlaceableObjectIndex>(indexerReference.IndexerEntity);
                    IncrementPrefabIndex(ref currentPrefabIndex, placeableObjectIndices.Length);
                    var nextPrefab = placeableObjectIndices[currentPrefabIndex];
                    var rotatedInstance = state.EntityManager.Instantiate(nextPrefab.Prefab);
                    state.EntityManager.GetComponentDataRW<LocalTransform>(rotatedInstance).ValueRW.Position = state.EntityManager.GetComponentData<LocalTransform>(buildInputData.SelectedEntity).Position;
                    state.EntityManager.SetComponentData(rotatedInstance, state.EntityManager.GetComponentData<PlaceableObjectData>(buildInputData.SelectedEntity));
                    buildInputData.BackupInstance = rotatedInstance;
                }
                else
                {
                    int length = state.EntityManager.GetBuffer<PlaceableObjectIndex>(buildInputData.PrefabIndexer).Length;
                    IncrementPrefabIndex(ref buildInputData.CurrentPrefabIndex, length);
                    CleanUpInstance(ref state, ref buildInputData.SelectedEntity);
                }
            }

            if ( buildInputData.DeleteInputPerformed
                 && buildInputData.SelectedEntity != Entity.Null
                 && state.EntityManager.TryGetComponent(buildInputData.SelectedEntity, out GameCurrency selectedEntityCurrencyValue) )
            {
                if ( selectedEntityCurrencyValue != 0 && localPlayerQuery.TryGetSingletonEntity<GameCurrency>(out var playerCurrencyEntity) )
                {
                    ref var playerCurrency = ref state.EntityManager.GetComponentDataRW<GameCurrency>(playerCurrencyEntity).ValueRW;
                    playerCurrency += selectedEntityCurrencyValue;
                }

                Entity prefabIndexer = buildInputData.PrefabIndexer;
                if ( buildInputData.IsInMoveMode )
                {
                    var lookup = SystemAPI.GetSingleton<PlaceableObjectIndexerLookupSystem.PlaceableObjectIndexerLookup>();
                    prefabIndexer = lookup.Value[state.EntityManager.GetComponentData<PrefabAssetID>(buildInputData.SelectedEntity)].IndexerEntity;
                }
                UpdateAvailableTrackedQuantity(ref state, 1, prefabIndexer);
                buildInputData.PrefabIndexer.Value = Entity.Null;
                buildInputData.DestroyEntityRequest = buildInputData.SelectedEntity;
                buildInputData.SelectedEntity = Entity.Null;
            }

            if ( buildInputData is { CreateEntityInPlace: true, IsInMoveMode: false }
                 && buildInputData.SelectedEntity != Entity.Null )
            {
                RestoreFromPreviewMode(ref state, buildInputData.SelectedEntity);
                if ( state.EntityManager.TryGetComponent<GameCurrency>(buildInputData.PrefabIndexer, out var prefabPurchasePrice) )
                {
                    state.EntityManager.SetComponentData(buildInputData.SelectedEntity, prefabPurchasePrice);
                }
                if ( UpdateAvailableTrackedQuantity(ref state, -1, buildInputData.PrefabIndexer) )
                {
                    Entity indexedPrefab = GetIndexedPrefab(ref state, in buildInputData);
                    var createdEntity = state.EntityManager.Instantiate(indexedPrefab);
                    state.EntityManager.SetComponentData(createdEntity, state.EntityManager.GetComponentData<LocalTransform>(buildInputData.SelectedEntity));
                    buildInputData.SelectedEntity = createdEntity;
                }
                else
                {
                    buildInputData.SelectedEntity = Entity.Null;
                    buildInputData.PrefabIndexer.Value = Entity.Null;
                }
            }
            buildInputData.CreateEntityInPlace = false;

            if ( buildInputData.CancelInputPerformed
                 && buildInputData.SelectedEntity != Entity.Null )
            {
                if ( buildInputData.IsInMoveMode )
                {
                    //restore old moveable
                    RestoreFromPreviewMode(ref state, buildInputData.SelectedEntity);
                    RestoreTransform(ref state, ref buildInputData);
                    backupInstanceExists = false;
                }
                else
                {
                    CancelFromBuildMode(ref state, ref buildInputData);
                }
            }

            if ( backupInstanceExists
                 && state.EntityManager.IsComponentEnabled<Simulate>(buildInputData.BackupInstance) )
            {
                CleanUpInstance(ref state, ref buildInputData.SelectedEntity);
                buildInputData.BeforeMovingTransform = state.EntityManager.GetComponentData<LocalTransform>(buildInputData.BackupInstance);
                buildInputData.SelectedEntity = buildInputData.BackupInstance;
            }

            if ( buildInputData.ClickInputPerformed
                 && buildInputData.CanBePlaced
                 && buildInputData.SelectedEntity != Entity.Null )
            {
                if ( buildInputData.IsInMoveMode )
                {
                    RestoreFromPreviewMode(ref state, buildInputData.SelectedEntity);
                    buildInputData.SelectedEntity = Entity.Null;
                    buildInputData.SelectedEntityForMove = Entity.Null;
                    buildInputData.ResetBackupState();
                }
                else if ( localPlayerQuery.TryGetSingletonEntity<GameCurrency>(out var localPlayerCurrencyEntity) )
                {
                    bool hasPurchasePrice = state.EntityManager.TryGetComponent<GameCurrency>(buildInputData.PrefabIndexer, out var placeablePurchasePrice);
                    if ( hasPurchasePrice
                         && CheckIfPlayerCanAffordPlaceable(localPlayerCurrencyEntity, placeablePurchasePrice, state.EntityManager, out var playerCurrency) )
                    {
                        state.EntityManager.SetComponentData(localPlayerCurrencyEntity, playerCurrency - placeablePurchasePrice);
                        buildInputData.CreateEntityInPlace = true;
                    }
                    else if ( !hasPurchasePrice )
                    {
                        buildInputData.CreateEntityInPlace = true;
                    }
                }
            }

            if ( buildInputData.ClickInputPerformed
                 && buildInputData.IsInMoveMode
                 && buildInputData.SelectedEntity == Entity.Null
                 && buildInputData.SelectedEntityForMove != Entity.Null )
            {
                buildInputData.BackupInstance = buildInputData.SelectedEntityForMove;
            }

            bool prefabHasChanged = buildInputData.PrefabIndexer.UpdateHasChanged();
            if ( prefabHasChanged )
            {
                buildInputData.CurrentPrefabIndex = 0;
                if ( backupInstanceExists )
                {
                    RestoreBackupInstance(ref state, ref buildInputData);
                }
            }

            if ( state.EntityManager.HasComponent<PlaceableObjectIndex>(buildInputData.PrefabIndexer)
                 && (prefabHasChanged || !state.EntityManager.Exists(buildInputData.SelectedEntity))
                 && TryCheckPlaceableableIsPurchaseable(localPlayerQuery, state.EntityManager, in buildInputData, out var localPlayerCurrency) )
            {
                bool hasTrackedQuantity = state.EntityManager.TryGetComponent(buildInputData.PrefabIndexer.Value, out TrackedQuantity trackedQuantity);
                if ( !hasTrackedQuantity || trackedQuantity.Available > 0 )
                {
                    float3? oldPosition = null;
                    if ( SelectedEntityIsValid(ref state, in buildInputData) )
                    {
                        oldPosition = state.EntityManager.GetComponentData<LocalTransform>(buildInputData.SelectedEntity).Position;
                        CleanUpInstance(ref state, ref buildInputData.SelectedEntity);
                    }
                    Entity indexedPrefab = GetIndexedPrefab(ref state, in buildInputData);
                    buildInputData.SelectedEntity = state.EntityManager.Instantiate(indexedPrefab);
                    if ( state.EntityManager.TryGetComponentRW<GameCurrency>(buildInputData.SelectedEntity, out var gameCurrencyRW) )
                    {
                        gameCurrencyRW.ValueRW.Value = 0;
                    }
                    if ( oldPosition.HasValue )
                    {
                        ref LocalTransform instanceTransform = ref state.EntityManager.GetComponentDataRW<LocalTransform>(buildInputData.SelectedEntity).ValueRW;
                        instanceTransform.Position = oldPosition.Value;
                    }
                }
                else
                {
                    CancelFromBuildMode(ref state, ref buildInputData);
                }
            }

            var selectedColor = _DefaultRendererColor;

            if ( SelectedEntityIsValid(ref state, in buildInputData) )
            {
                ApplyObstructionColor(ref state, ref buildInputData, ref selectedColor, localPlayerQuery, !localPlayerQuery.IsEmpty);
                ApplyColorIfDifferent(ref state, buildInputEntity, selectedColor);
            }
            if ( SelectedEntityIsValid(ref state, in buildInputData)
                 && state.EntityManager.IsComponentEnabled<Simulate>(buildInputData.SelectedEntity) )
            {
                SetUpEntityPreviewMode(ref state, buildInputData.SelectedEntity);
            }
            ApplyBuildInputChanges(ref state, buildInputEntity, in buildInputData);
        }

        private static void CancelFromBuildMode(ref SystemState state, ref BuildInputData buildInputData)
        {
            buildInputData.PrefabIndexer.Value = Entity.Null;
            CleanUpInstance(ref state, ref buildInputData.SelectedEntity);
            buildInputData.ResetBackupState();
        }

        /// <summary>
        /// update the quantities of a selected prefab indexer
        /// </summary>
        /// <param name="state"></param>
        /// <param name="quantityChange"></param>
        /// <param name="indexerEntity"></param>
        /// <returns>whatever or not the production of new build object can continue</returns>
        private static bool UpdateAvailableTrackedQuantity(ref SystemState state, int quantityChange, Entity indexerEntity)
        {
            if ( !state.EntityManager.TryGetComponentRW<TrackedQuantity>(indexerEntity, out var trackedQuantityRW) )
                return true;

            trackedQuantityRW.ValueRW.Available = (uint)math.max((long)trackedQuantityRW.ValueRW.Available + quantityChange, 0);
            return trackedQuantityRW.ValueRO.Available > 0;
        }

        private static bool HasBackupInstance(ref SystemState state, in BuildInputData buildInputData)
        {
            return state.EntityManager.HasComponent<LocalTransform>(buildInputData.BackupInstance);
        }

        private static void RevertAndResetBuildInputData(ref SystemState state, bool backupInstanceExists, ref BuildInputData buildInputData, Entity buildInputEntity)
        {
            if ( backupInstanceExists )
            {
                RestoreBackupInstance(ref state, ref buildInputData);
            }
            else
            {
                CleanUpInstance(ref state, ref buildInputData.SelectedEntity);
            }
            buildInputData = default;
        }

        private Entity GetBuildInputEntity()
        {
            var entityArray = _placementInputDataChangedQuery.ToEntityArray(Allocator.Temp);
#if UNITY_EDITOR
            if ( entityArray.Length != 1 ) // this isn't a singleton component because singleton are not allowed with an enableable component 
            {
                Debug.LogError($"exactly one entity should be returned for this query, actual count= {entityArray.Length} ");
                return entityArray.Length == 0 ? Entity.Null : entityArray[0];
            }
#endif
            return entityArray[0];
        }

        private static void RestoreBackupInstance(ref SystemState state, ref BuildInputData buildInputData)
        {
            RestoreFromPreviewMode(ref state, buildInputData.BackupInstance);
            RestoreTransform(ref state, ref buildInputData);
        }

        public static void RestoreTransform(ref SystemState state, ref BuildInputData buildInputData)
        {
            state.EntityManager.SetComponentData(buildInputData.SelectedEntity, buildInputData.BeforeMovingTransform);
            buildInputData.SelectedEntity = Entity.Null;
            buildInputData.ResetBackupState();
        }

        private static bool SelectedEntityIsValid(ref SystemState state, in BuildInputData buildInputData)
        {
            return buildInputData.SelectedEntity != Entity.Null
                   && state.EntityManager.HasComponent<Simulate>(buildInputData.SelectedEntity);
        }

        private static void ApplyColorIfDifferent(ref SystemState state, Entity buildInputEntity, Color selectedColor)
        {
            var componentDataRW = state.EntityManager.GetComponentDataRW<ApplyColorData>(buildInputEntity);
            if ( !(componentDataRW.ValueRW.Color == selectedColor) )
            {
                componentDataRW.ValueRW.Color = selectedColor;
                state.EntityManager.SetComponentEnabled<ApplyColorData>(buildInputEntity, true);
            }
        }

        private static void IncrementPrefabIndex(ref int currentPrefabIndex, int length)
        {
            currentPrefabIndex = (currentPrefabIndex + 1) % length;
        }

        private static Entity GetIndexedPrefab(ref SystemState state, in BuildInputData buildInputData)
        {
            var placeableObjectIndices = state.EntityManager.GetBuffer<PlaceableObjectIndex>(buildInputData.PrefabIndexer);
            return placeableObjectIndices[buildInputData.CurrentPrefabIndex].Prefab;
        }

        private static void ApplyBuildInputChanges(ref SystemState state, Entity placementInputEntity, in BuildInputData buildInputData)
        {
            state.EntityManager.SetComponentData(placementInputEntity, buildInputData);
        }

        private void ApplyObstructionColor(ref SystemState state, ref BuildInputData buildInputData, ref Color selectedColor, EntityQuery localPlayerQuery, bool hasLocalPlayer)
        {
            if ( !buildInputData.CanBePlaced )
                // obstructed: show cannot build feedback
            {
                selectedColor = _placementSettingsData.ObstructedPlacementColor;
            }
            else if ( !buildInputData.IsInMoveMode )
            {
                if ( !hasLocalPlayer )
                {
                    selectedColor = _placementSettingsData.ObstructedPlacementColor;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryCheckPlaceableableIsPurchaseable(EntityQuery localPlayerQuery, EntityManager entityManager, in BuildInputData buildInputData, out GameCurrency localPlayerCurrency)
        {
            return localPlayerQuery.TryGetSingleton(out localPlayerCurrency)
                   && entityManager.TryGetComponent<GameCurrency>(buildInputData.PrefabIndexer, out var selectedPurchasePrice)
                   && CanPlayerAffordPlaceable(localPlayerCurrency, selectedPurchasePrice);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckIfPlayerCanAffordPlaceable(Entity localPlayerCurrencyEntity, GameCurrency purchasePrice, EntityManager entityManager, out GameCurrency localPlayerCurrency)
        {
            localPlayerCurrency = entityManager.GetComponentData<GameCurrency>(localPlayerCurrencyEntity);
            return CanPlayerAffordPlaceable(localPlayerCurrency, purchasePrice);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanPlayerAffordPlaceable(GameCurrency localPlayerCurrency, GameCurrency purchasePrice)
        {
            return localPlayerCurrency >= purchasePrice;
        }

        public static void RestoreFromPreviewMode(ref SystemState state, Entity instanceToRestore)
        {
            state.EntityManager.SetComponentEnabled<Simulate>(instanceToRestore, true);
        }

        public static void SetUpEntityPreviewMode(ref SystemState state, Entity instance)
        {
            state.EntityManager.SetComponentEnabled<Simulate>(instance, false);
        }

        private static void CleanUpInstance(ref SystemState state, ref Entity instance)
        {
            if ( state.EntityManager.Exists(instance) )
            {
                state.EntityManager.DestroyEntity(instance);
                instance = Entity.Null;
            }
        }

        [BurstCompile]
        public void OnStopRunning(ref SystemState state)
        {
            Entity buildInputEntity = GetBuildInputEntity();
            ref var buildInputData = ref state.EntityManager.GetComponentDataRW<BuildInputData>(buildInputEntity).ValueRW;
            bool backupInstanceExists = HasBackupInstance(ref state, in buildInputData);
            RevertAndResetBuildInputData(ref state, backupInstanceExists, ref buildInputData, buildInputEntity);
        }
    }

    internal partial struct ApplyRendererColorSystem : ISystem
    {
        void ISystem.OnUpdate(ref SystemState state)
        {
            foreach ( var (buildInputData, applyColorData, enabledApplyColorData)
                     in SystemAPI.Query<BuildInputData, ApplyColorData, EnabledRefRW<ApplyColorData>>() )
            {
                ApplyColorToPlacingObject(ref state, buildInputData.SelectedEntity, applyColorData.Color);
                enabledApplyColorData.ValueRW = false;
            }
        }

        private void ApplyColorToPlacingObject(ref SystemState state, Entity placingRootEntity, Color color)
        {
            var renderers = state.EntityManager.GetBuffer<ChildRenderers>(placingRootEntity);
            for ( var index = 0; index < renderers.Length; index++ )
            {
                var childRenderer = renderers[index];
                state.EntityManager.GetComponentObject<SpriteRenderer>(childRenderer.Entity).color = color;
            }
        }
    }

    public struct ApplyColorData : IComponentData, IEnableableComponent
    {
        public Color Color;
    }
}