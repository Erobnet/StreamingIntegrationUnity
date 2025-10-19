using Drboum.Utilities.Entities;
using GameProject.Persistence;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace GameProject.WorldObjectPlacement
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public unsafe partial struct PlaceableObjectIndexerLookupSystem : ISystem
    {
        public struct PlaceableObjectIndexerLookup : IComponentData
        {
            public NativeHashMap<PrefabAssetID, IndexerReference> Value;
        }

        public struct IndexerReference
        {
            public EntityWith<PlaceableObjectIndex> IndexerEntity;
            public int IndexInList;
        }

        private PlaceableObjectIndexerLookup _placeableObjectIndexerLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _placeableObjectIndexerLookup = new PlaceableObjectIndexerLookup {
                Value = new(10, Allocator.Persistent)
            };
            state.EntityManager.AddComponentData(state.SystemHandle, _placeableObjectIndexerLookup);
        }

        [BurstCompile]
        void ISystem.OnUpdate(ref SystemState state)
        {
            var hasRun = false;
            foreach ( var (indexers, entity)
                     in SystemAPI.Query<DynamicBuffer<PlaceableObjectIndex>>().WithEntityAccess()
                         .WithChangeFilter<PlaceableObjectIndex>() )
            {
                for ( var index = 0; index < indexers.Length; index++ )
                {
                    var placeableObjectIndex = indexers[index];
                    _placeableObjectIndexerLookup.Value[state.EntityManager.GetComponentData<PrefabAssetID>(placeableObjectIndex.Prefab)] = new() {
                        IndexerEntity = entity,
                        IndexInList = index
                    };
                }
                hasRun = true;
            }
            if ( hasRun )
            {
                state.EntityManager.SetComponentData(state.SystemHandle, _placeableObjectIndexerLookup);
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _placeableObjectIndexerLookup.Value.Dispose();
            state.EntityManager.RemoveComponent<PlaceableObjectIndexerLookup>(state.SystemHandle);

        }
    }
}