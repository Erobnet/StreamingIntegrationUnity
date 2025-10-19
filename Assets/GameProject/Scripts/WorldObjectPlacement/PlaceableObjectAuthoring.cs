using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Drboum.Utilities.Entities;
using Drboum.Utilities.Runtime.EditorHybrid;
using GameProject.Common.Baking;
using GameProject.Persistence;
using GameProject.Persistence.CommonData;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Hybrid;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;

namespace GameProject.WorldObjectPlacement
{
    [RequireComponent(typeof(BoxCollider))]
    public class PlaceableObjectAuthoring : PrefabIdentity, IDeclareBakeDependencies
    {
        [SerializeField] private PlacementSettingsAuthoring placementSettings;
        [SerializeField] private SpriteRenderer[] renderers = Array.Empty<SpriteRenderer>();
        [SerializeField, HideInInspector] internal bool CanBeDeleted = true;
        [SerializeField] private BoxCollider boxCollider;
        [SerializeField] private NavMeshObstacle navMeshObstacle;
        [SerializeField] private LayerMask placementCollisionMask;
        [SerializeField] private LayerMask surfaceCollisionMask;
        [SerializeField] private PlacementRelativeTo allowedPlacementRelativeToSurface = PlacementRelativeTo.Top | PlacementRelativeTo.Left | PlacementRelativeTo.Right | PlacementRelativeTo.Front;
        [SerializeField] private PlacementRelativeTo snappingPreferenceRelativeToOtherBuildObjects = PlacementRelativeTo.Horizontal;
        public IReadOnlyList<SpriteRenderer> Renderers => renderers;



        private void OnValidate()
        {
            bool hasChanged = false;
            if ( boxCollider == null )
            {
                boxCollider = GetComponent<BoxCollider>();
                hasChanged = true;
            }
            UpdateNavmeshDataFromCollider();

            var newRenderers = GetComponentsInChildren<SpriteRenderer>();
            if ( newRenderers.Length != Renderers.Count )
            {
                renderers = newRenderers;
                hasChanged = true;
            }
            if ( hasChanged )
            {
                this.SetDirtySafe();
            }
        }

        [ContextMenu(nameof(AssignDefaultMaskValueFromPlaceableSettings))]
        private void AssignDefaultMaskValues()
        {
            AssignDefaultMaskValueFromPlaceableSettings(ref placementCollisionMask, placementSettings.PlaceableObjectSettings.PlacementCollisionMasks);
            AssignDefaultMaskValueFromPlaceableSettings(ref surfaceCollisionMask, placementSettings.PlaceableObjectSettings.SurfaceCollisionMasks);
            this.SetDirtySafe();
        }

        private void AssignDefaultMaskValueFromPlaceableSettings(ref LayerMask collisionMask, LayerMask defaultMasks)
        {
            collisionMask.value = defaultMasks.value;
        }

        [ContextMenu(nameof(UpdateNavmeshDataFromCollider))]
        private void UpdateNavmeshDataFromCollider()
        {
            if ( !navMeshObstacle )
                return;

            var navMeshObstacleHasChanged = false;
            if ( navMeshObstacle.center != boxCollider.center )
            {
                navMeshObstacle.center = boxCollider.center;
                navMeshObstacleHasChanged = true;
            }
            if ( navMeshObstacle.size != boxCollider.size )
            {
                navMeshObstacle.size = boxCollider.size;
                navMeshObstacleHasChanged = true;
            }
            if ( navMeshObstacle.shape != NavMeshObstacleShape.Box )
            {
                navMeshObstacle.shape = NavMeshObstacleShape.Box;
                navMeshObstacleHasChanged = true;
            }

            if ( navMeshObstacleHasChanged )
            {
                navMeshObstacle.SetDirtySafe();
            }
        }

        protected class PlaceableObjectAuthoringBaker : Baker<PlaceableObjectAuthoring>
        {
            public override void Bake(PlaceableObjectAuthoring authoring)
            {
                this.BakeAuthoringWith<PrefabIdentityBaker>();
#if UNITY_EDITOR
                authoring.UpdateNavmeshDataFromCollider();
                DependsOn(authoring.boxCollider);
                DependsOn(authoring.navMeshObstacle);
#endif
                var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
                AddComponent(entity, new PlaceableObjectData {
                    PlacementObstructionMask = (uint)authoring.placementCollisionMask.value,
                    PlacementSurfaceMask = (uint)authoring.surfaceCollisionMask.value,
                    AllowedPlacementRelativeToSurface = authoring.allowedPlacementRelativeToSurface,
                    SnappingPreferenceRelativeToOtherBuildObjects = authoring.snappingPreferenceRelativeToOtherBuildObjects
                });
                if ( authoring.CanBeDeleted )
                {
                    AddComponent<GameCurrency>(entity);
                }
                var rendererBuffer = AddBuffer<ChildRenderers>(entity);
                for ( var index = 0; index < authoring.Renderers.Count; index++ )
                {
                    rendererBuffer.Add(new ChildRenderers { Entity = GetEntity(authoring.Renderers[index], TransformUsageFlags.None) });
                }
            }
        }

        public void DeclareDependencies(IBaker baker)
        {
            baker.DependsOn(boxCollider);
            baker.DependsOn(navMeshObstacle);
        }
    }

    [InternalBufferCapacity(0)]
    public struct ChildRenderers : IBufferElementData
    {
        public EntityWith<SpriteRenderer> Entity;
    }

    public struct PlaceableObjectData : IComponentData
    {
        public uint PlacementObstructionMask;
        public uint PlacementSurfaceMask;
        public PlacementRelativeTo AllowedPlacementRelativeToSurface;
        public PlacementRelativeTo SnappingPreferenceRelativeToOtherBuildObjects;
    }

    [Flags]
    public enum PlacementRelativeTo : byte
    {
        Top = 1 << 0,
        Left = 1 << 1,
        Right = 1 << 2,
        Front = 1 << 3,
        Horizontal = Left | Right,
    }

    public static class PlacementRelativeToExtensions
    {
        public static void FillDirectionList(this PlacementRelativeTo placementRelativeTo, NativeList<float3> directions, quaternion axis)
        {
            if ( placementRelativeTo.HasFlagNoAlloc(PlacementRelativeTo.Top) )
            {
                directions.Add(math.up());
            }
            if ( placementRelativeTo.HasFlagNoAlloc(PlacementRelativeTo.Left) )
            {
                directions.Add(math.mul(axis, math.left()));
            }
            if ( placementRelativeTo.HasFlagNoAlloc(PlacementRelativeTo.Right) )
            {
                directions.Add(math.mul(axis, math.right()));
            }
            if ( placementRelativeTo.HasFlagNoAlloc(PlacementRelativeTo.Front) )
            {
                directions.Add(-math.forward(axis));
            }
        }
    }
}