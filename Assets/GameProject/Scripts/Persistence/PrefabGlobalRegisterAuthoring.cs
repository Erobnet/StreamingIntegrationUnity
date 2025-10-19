using System;
using System.Collections.Generic;
using Drboum.Utilities.Runtime;
using Drboum.Utilities.Runtime.EditorHybrid;
using GameProject.Common.Baking;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Profiling;
using Unity.Scenes;
using UnityEngine;

namespace GameProject.Persistence
{
    public class PrefabGlobalRegisterAuthoring : MonoBehaviour
    {
        [SerializeField] private List<PrefabIdentity> prefabs;
        [SerializeField] private List<PrefabIdentity> excludePrefabs;
        private readonly HashSet<PrefabIdentity> _excludePrefabsSet = new();

        private class Baker : Baker<PrefabGlobalRegisterAuthoring>
        {
            public override void Bake(PrefabGlobalRegisterAuthoring authoring)
            {
#if UNITY_EDITOR
                var prefabs = new List<PrefabIdentity>(authoring.prefabs.Count);
                //build excludeset 
                authoring._excludePrefabsSet.Clear();
                foreach ( var excludePrefab in authoring.excludePrefabs )
                {
                    authoring._excludePrefabsSet.Add(excludePrefab);
                }
                UnityObjectEditorHelper.FindAllComponentInPrefabs(prefabs, lookupFolders: new[] { "Assets/GameProject" });
                //check for prefabs to exclude
                for ( var index = prefabs.Count - 1; index >= 0; index-- )
                {
                    var prefabIdentity = prefabs[index];
                    if ( !prefabIdentity || authoring._excludePrefabsSet.Contains(prefabIdentity) )
                    {
                        prefabs.RemoveAt(index);
                    }
                }

                var hasChanged = prefabs.Count != authoring.prefabs.Count;
                for ( var index = 0; index < authoring.prefabs.Count && !hasChanged; index++ )
                {
                    hasChanged = authoring.prefabs[index] != prefabs[index];
                }
                authoring.prefabs = prefabs;

                if ( hasChanged )
                {
                    authoring.SetDirtySafe();
                }
#endif
                var buffer = AddBuffer<PrefabAssetRegisterData>(GetEntity(TransformUsageFlags.None));
                for ( var index = 0; index < authoring.prefabs.Count; index++ )
                {
                    var prefabIdentity = authoring.prefabs[index];
                    DependsOn(prefabIdentity);
                    DependsOn(prefabIdentity.gameObject);
                    if ( prefabIdentity is IDeclareBakeDependencies declareBakeDependencies )
                    {
                        declareBakeDependencies.DeclareDependencies(this);
                    }
                    var prefabEntity = GetEntity(prefabIdentity, TransformUsageFlags.None);
                    buffer.Add(new PrefabAssetRegisterData {
                        AssetID = new PrefabAssetID(prefabIdentity.Guid),
                        Prefab = prefabEntity
                    });
                }
            }
        }
    }

    public class PrefabIdentityBaker : Baker<PrefabIdentity>
    {
        public override void Bake(PrefabIdentity authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent(entity, new PrefabAssetID(authoring.Guid));
        }
    }

    public readonly struct PrefabAssetID : IComponentData, IEquatable<PrefabAssetID>
    {
        public readonly int Value;

        public PrefabAssetID(in GuidWrapper guidWrapper)
        {
            Value = guidWrapper.GetHashCode();
        }

        public bool Equals(PrefabAssetID other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is PrefabAssetID other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public static bool operator ==(PrefabAssetID left, PrefabAssetID right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PrefabAssetID left, PrefabAssetID right)
        {
            return !left.Equals(right);
        }
    }

    public struct PrefabAssetRegisterData : IBufferElementData
    {
        public PrefabAssetID AssetID;
        public Entity Prefab;
    }

    public struct PrefabGlobalRegisterLookup : IComponentData
    {
        public NativeHashMap<PrefabAssetID, Entity> Value;
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(SceneSystemGroup))]
    public partial struct PrefabGlobalRegisterSystem : ISystem, ISystemStartStop
    {
        private PrefabGlobalRegisterLookup _prefabGlobalRegisterLookup;
        private EntityQuery _prefabAssetToRegister;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _prefabGlobalRegisterLookup.Value = new(10, Allocator.Persistent);
            _prefabAssetToRegister = SystemAPI.QueryBuilder()
                .WithAll<PrefabAssetRegisterData>()
                .Build();
            state.RequireForUpdate(_prefabAssetToRegister);
        }

        [BurstCompile]
        public void OnStartRunning(ref SystemState state)
        {
            state.EntityManager.AddComponentData(state.SystemHandle, _prefabGlobalRegisterLookup);
        }

        [BurstCompile]
        void ISystem.OnUpdate(ref SystemState state)
        {
            _prefabAssetToRegister.SetChangedVersionFilter<PrefabAssetRegisterData>();
            if ( _prefabAssetToRegister.IsEmpty )
                return;

            _prefabGlobalRegisterLookup.Value.Clear();
            _prefabAssetToRegister.ResetFilter();

            new RegisterPrefabJob {
                PrefabGlobalRegisterLookup = _prefabGlobalRegisterLookup
            }.Run(_prefabAssetToRegister);
        }

        [BurstCompile]
        [WithOptions(EntityQueryOptions.IncludePrefab)]
        public partial struct RegisterPrefabJob : IJobEntity
        {
            private static readonly ProfilerMarker _ExecuteMarker = new(nameof(RegisterPrefabJob) + "Marker");

            public PrefabGlobalRegisterLookup PrefabGlobalRegisterLookup;

            private void Execute(in DynamicBuffer<PrefabAssetRegisterData> prefabAssetRegisterDatas)
            {
                _ExecuteMarker.Begin();
                for ( var index = 0; index < prefabAssetRegisterDatas.Length; index++ )
                {
                    var prefabAssetRegisterData = prefabAssetRegisterDatas[index];
                    PrefabGlobalRegisterLookup.Value[prefabAssetRegisterData.AssetID] = prefabAssetRegisterData.Prefab;
                }
                _ExecuteMarker.End();
            }
        }

        public void OnDestroy(ref SystemState state)
        {
            _prefabGlobalRegisterLookup.Value.Dispose();
        }

        public void OnStopRunning(ref SystemState state)
        {
            state.EntityManager.RemoveComponent<PrefabGlobalRegisterLookup>(state.SystemHandle);
        }
    }
}