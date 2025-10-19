using System.Linq;
using Drboum.Utilities.Entities;
using GameProject.Persistence;
using GameProject.Persistence.CommonData;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace GameProject.WorldObjectPlacement
{
    public class PlacementSettingsAuthoring : MonoBehaviour
    {
        [SerializeField] private PlaceableObjectSettings placeableObjectSettings;
        public PlaceableObjectSettings PlaceableObjectSettings => placeableObjectSettings;

        private class Baker : Baker<PlacementSettingsAuthoring>
        {
            public override void Bake(PlacementSettingsAuthoring authoring)
            {
                DependsOn(authoring.placeableObjectSettings);
                var entity = GetEntity(authoring, TransformUsageFlags.None);

                var availableBuildObjects = AddBuffer<BuildObjectCollection>(entity);
                var placeableObjectPrefabs = authoring.placeableObjectSettings.PlaceableObjectPrefabs;
                for ( var index = 0; index < placeableObjectPrefabs.Count; index++ )
                {
                    var placeableIndexerAuthoring = placeableObjectPrefabs[index];
                    DependsOn(placeableIndexerAuthoring);
                    var prefabEntity = CreateAdditionalEntity(TransformUsageFlags.None, entityName: $"{authoring.placeableObjectSettings.name}_{placeableIndexerAuthoring.name}");
                    BakePlaceableIndexer(placeableIndexerAuthoring, prefabEntity);
                    availableBuildObjects.Add(new() {
                        PrefabCollection = prefabEntity,
                    });
                }

                var blobBuilder = new BlobBuilder(Allocator.Temp);
                ref var root = ref blobBuilder.ConstructRoot<GridSettingsData>();
                root.DefaultStartPoint = authoring.placeableObjectSettings.StartPoint;
                blobBuilder.Construct(ref root.SnapGridCellSizes, authoring.placeableObjectSettings.SnapCellSizes.Select(snapGridCellSize => new SnapGridCellSize {
                    Value = snapGridCellSize
                }).ToArray());
                var blobAssetReference = blobBuilder.CreateBlobAssetReference<GridSettingsData>(Allocator.Persistent);
                AddBlobAsset(ref blobAssetReference, out _);
                AddComponent(entity, new GridSettingsRef {
                    Ref = blobAssetReference
                });
                AddComponent(entity, new GridRuntimeData {
                    StartPoint = authoring.placeableObjectSettings.StartPoint,
                });
                AddComponent(entity, new PlacementSettingsData {
                    SurfaceCollisionMask = (uint)authoring.placeableObjectSettings.SurfaceCollisionMasks.value,
                    MoveableCollisionMask = (uint)authoring.placeableObjectSettings.MoveableCollisionMask.value,
                    ObstructedPlacementColor = authoring.placeableObjectSettings.ObstructedPlacementColor,
                });
            }

            private void BakePlaceableIndexer(PlaceableObjectIndexer authoring, Entity indexerEntity)
            {
                AddComponent(indexerEntity, new BuildObjectData {
                    DisplayIcon = authoring.DisplayIcon,
                });
                if ( authoring.AvailableStock > 0 )
                {
                    AddComponent(indexerEntity, new PersistenceInstanceId(authoring.Guid));
                    AddComponent(indexerEntity, new TrackedQuantity {
                        Available = (uint)authoring.AvailableStock,
                    });
                }
                if ( authoring.CanBeDeleted )
                    AddComponent(indexerEntity, authoring.PurchasePrice);

                var buffer = AddBuffer<PlaceableObjectIndex>(indexerEntity);
                foreach ( var placeableObjectAuthoring in authoring.PlaceableObjectOptions )
                {
                    DependsOn(placeableObjectAuthoring);
                    buffer.Add(new PlaceableObjectIndex {
                        Prefab = GetEntity(placeableObjectAuthoring, TransformUsageFlags.Dynamic),
                    });
                }
            }
        }
    }

    public struct GridSettingsRef : IComponentData
    {
        public BlobAssetReference<GridSettingsData> Ref;
    }

    public struct GridRuntimeData : IComponentData
    {
        public float3 StartPoint;
        public int CurrentSnapIndex;
        public SnapGridCellSize CurrentSnapping;
    }

    public struct GridSettingsData
    {
        public float3 DefaultStartPoint;
        public BlobArray<SnapGridCellSize> SnapGridCellSizes;
    }

    public struct SnapGridCellSize : IBufferElementData
    {
        public float3 Value;
    }

    public struct PlacementSettingsData : IComponentData
    {
        public uint SurfaceCollisionMask;
        public uint MoveableCollisionMask;
        public Color ObstructedPlacementColor;
    }

    public struct BuildObjectCollection : IBufferElementData
    {
        public EntityWith<PlaceableObjectIndex> PrefabCollection;
    }

    public struct BuildObjectData : IComponentData
    {
        public UnityObjectRef<Sprite> DisplayIcon;
    }

    public struct TrackedQuantity : IComponentData
    {
        public uint Available;
    }


    public struct BuildInputData : IComponentData, IEnableableComponent
    {
        public ChangeTracker<Entity> PrefabIndexer;
        public Entity SelectedEntity;
        public Entity DestroyEntityRequest;
        public Entity BackupInstance;
        public Entity SelectedEntityForMove;

        public bool CanBePlaced;
        public bool CreateEntityInPlace;
        public int CurrentPrefabIndex;
        public LocalTransform BeforeMovingTransform;
        public bool ClickInputPerformed;
        public bool DeleteInputPerformed;
        public bool CancelInputPerformed;
        public bool RotateBuildPerformed;

        public bool IsInMoveMode => PrefabIndexer.Value == Entity.Null;

        public void ResetBackupState()
        {
            BackupInstance = Entity.Null;
            BeforeMovingTransform = default;
        }
    }
}