using System;
using System.Runtime.CompilerServices;
using Drboum.Utilities.Entities;
using Drboum.Utilities.Runtime;
using GameProject.CameraManagement;
using Unity.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Extensions;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using BoxCollider = Unity.Physics.BoxCollider;
using Collider = Unity.Physics.Collider;
using Ray = UnityEngine.Ray;
using RaycastHit = Unity.Physics.RaycastHit;

namespace GameProject.WorldObjectPlacement
{
    [UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
    public unsafe partial struct ObjectPlacementPhysicsQuerySystem : ISystem, ISystemStartStop
    {
        private const uint _EVERYTHING_MASK = ~0u;
        private const float _MINIMUM_DISTANCE_FOR_POS_CHANGE = .01f;
        private const float _MINIMUM_DISTANCE_FOR_POS_CHANGE_SQ = _MINIMUM_DISTANCE_FOR_POS_CHANGE;
        private const float _CLOSE_IN_DIRECTION_DOT_PRODUCT_THRESHOLD_THRESHOLD = .9f;
        private EntityQuery _buildInputQuery;
        private GridSettingsRef _gridSettingsRef;
        private PlacementSettingsData _placementSettingsData;


        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GridRuntimeData>();
            state.RequireForUpdate<PlacementSettingsData>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<GridSettingsRef>();
            _buildInputQuery = SystemAPI.QueryBuilder()
                .WithPresent<BuildInputData>()
                .Build();
            state.RequireForUpdate(_buildInputQuery);
            state.EntityManager.AddComponent<RunnableOncePerFrame>(state.SystemHandle);
        }

        [BurstCompile]
        public void OnStartRunning(ref SystemState state)
        {
            _gridSettingsRef = SystemAPI.GetSingleton<GridSettingsRef>();
            _placementSettingsData = SystemAPI.GetSingleton<PlacementSettingsData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if ( !SystemRunnableEnablerSystem.IsSystemRunnableThisFrame(ref state) )
                return;
#if UNITY_EDITOR
            _gridSettingsRef = SystemAPI.GetSingleton<GridSettingsRef>();
            _placementSettingsData = SystemAPI.GetSingleton<PlacementSettingsData>();
#endif
            var buildInputEntity = _buildInputQuery.ToEntityArray(Allocator.Temp)[0];
            ref var buildInputData = ref state.EntityManager.GetComponentDataRW<BuildInputData>(buildInputEntity).ValueRW;
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var mainCameraRayEntity = SystemAPI.QueryBuilder()
                .WithAll<RayData, MainCameraTag>()
                .WithOptions(EntityQueryOptions.IncludeSystems)
                .Build()
                .GetSingletonEntity();

            var mainCameraRay = state.EntityManager.GetComponentData<RayData>(mainCameraRayEntity).Value;
            var mainCameraPosRotation = state.EntityManager.GetComponentData<PositionRotationData>(mainCameraRayEntity);
            ref var gridSettingsData = ref _gridSettingsRef.Ref.Value;
            ref var gridRuntimeData = ref SystemAPI.GetSingletonRW<GridRuntimeData>().ValueRW;
            SetDefaultGridSnappingValue(ref gridRuntimeData, ref gridSettingsData);
            var raycastInput = CreateRaycastInputFromRay(mainCameraRay, 100);
            raycastInput.Filter.CollidesWith = _placementSettingsData.MoveableCollisionMask;
            var castResults = new NativeList<RaycastHit>(Allocator.Temp);
            buildInputData.SelectedEntityForMove = Entity.Null;
            TryQuerySelectablePhysicsEntity(physicsWorld, raycastInput, castResults, in buildInputData, ref buildInputData.SelectedEntityForMove);
            if ( !state.EntityManager.HasComponent<Simulate>(buildInputData.SelectedEntity) )
                return;

            var placeableObjectData = state.EntityManager.GetComponentData<PlaceableObjectData>(buildInputData.SelectedEntity);
            var selectedEntityTransform = state.EntityManager.GetComponentData<LocalTransform>(buildInputData.SelectedEntity);
            var modifiedSelectedEntityTransform = selectedEntityTransform;

            var physicsCollider = state.EntityManager.GetComponentData<PhysicsCollider>(buildInputData.SelectedEntity);
            var selectedEntityGeometry = GetScaledBoxGeometry(ref physicsCollider.Value.Value, selectedEntityTransform.Scale);
            var allowedDirectionsForSurface = new NativeList<float3>(8, Allocator.Temp);
            placeableObjectData.AllowedPlacementRelativeToSurface.FillDirectionList(allowedDirectionsForSurface, mainCameraPosRotation.Rotation);
            var filterMaskHitCollector = new FilterSurfaceRaycastHitCollector(allowedDirectionsForSurface.AsArray(), physicsWorld.Bodies, placeableObjectData.PlacementSurfaceMask, buildInputData.SelectedEntity);
            raycastInput.Filter.CollidesWith = placeableObjectData.PlacementSurfaceMask | _placementSettingsData.SurfaceCollisionMask;
            bool foundSurface = physicsWorld.CollisionWorld.CastRay(raycastInput, ref filterMaskHitCollector);
            bool isPlacementObstructed = true;
            bool foundValidPlacementSurface = false;
            var surfaceRaycastHit = filterMaskHitCollector.ClosestHit;
            if ( foundSurface )
            {
                RigidBody surfaceBody = physicsWorld.Bodies[surfaceRaycastHit.RigidBodyIndex];
                float3 startPointOverride = GetCenterPointInWorldSpace(in surfaceBody, out var surfaceScaledGeometry);
                float3 surfaceGeometryHalfSize = (surfaceScaledGeometry.Size * .5f);
                var halfSelectedEntitySize = ((selectedEntityGeometry.Size) * .5f);
                Assert.IsTrue(math.lengthsq(surfaceRaycastHit.SurfaceNormal) <= 1 + float.Epsilon, "Surface normal must be normalized");
                var offsetAlignedWithNormal = ((surfaceGeometryHalfSize + surfaceScaledGeometry.BevelRadius) * surfaceRaycastHit.SurfaceNormal);
                //currently this only supports normals with dominant angles in one world space axis, could probably be extended to support all cases but this game is not going to have those cases
                var normalizer = math.round(new float3(1f) - math.abs(surfaceRaycastHit.SurfaceNormal));
                startPointOverride -= (surfaceGeometryHalfSize * normalizer);
                startPointOverride += offsetAlignedWithNormal;

                var snapCellSize = selectedEntityGeometry.Size;
                float3 centerOffsetRelativeToRot = math.mul(modifiedSelectedEntityTransform.Rotation, selectedEntityGeometry.Center);
                var centerOffsetFromStartPoint = centerOffsetRelativeToRot + halfSelectedEntitySize;
                float3 snappedPos = (math.round((surfaceRaycastHit.Position - startPointOverride) / snapCellSize) * snapCellSize);
                modifiedSelectedEntityTransform.Position = (startPointOverride + snappedPos + centerOffsetFromStartPoint) - centerOffsetRelativeToRot;

                foundValidPlacementSurface = filterMaskHitCollector.NumHits == 1;
                if ( foundValidPlacementSurface )
                {
                    float selectedBevel = selectedEntityGeometry.BevelRadius;
                    var overlapAabbInput = new OverlapAabbInput {
                        Filter = new() {
                            BelongsTo = _EVERYTHING_MASK,
                            CollidesWith = placeableObjectData.PlacementObstructionMask,
                        },
                        Aabb = AABBComponent.Create(modifiedSelectedEntityTransform.Position + centerOffsetRelativeToRot, selectedEntityGeometry.Size)
                    };
                
                    var resultIndexlist = new NativeList<int>(Allocator.Temp);
                    isPlacementObstructed = physicsWorld.OverlapAabb(overlapAabbInput, ref resultIndexlist)
                                            && !(resultIndexlist.Length == 1 && physicsWorld.Bodies[resultIndexlist[0]].Entity == buildInputData.SelectedEntity);
                
                    if ( isPlacementObstructed )
                    {
                        var selectedBody = default(RigidBody);
                        var shortestDistance = float.MaxValue;
                        for ( var index = 0; index < resultIndexlist.Length; index++ )
                        {
                            int resultIndex = resultIndexlist[index];
                            var body = physicsWorld.Bodies[resultIndex];
                            if ( body.Entity == buildInputData.SelectedEntity )
                                continue;
                
                            float distancesq = math.distancesq(body.WorldFromBody.pos, surfaceRaycastHit.Position);
                            if ( distancesq < shortestDistance )
                            {
                                shortestDistance = distancesq;
                                selectedBody = body;
                            }
                        }
                
                        startPointOverride = GetCenterPointInWorldSpace(in selectedBody, out var scaledGeometry);
                        startPointOverride.xy -= ((scaledGeometry.Size / 2f).xy);
                        halfSelectedEntitySize += (selectedBevel + scaledGeometry.BevelRadius);
                        float3 offset = GetSnappingOffset(placeableObjectData.SnappingPreferenceRelativeToOtherBuildObjects, scaledGeometry.Size, halfSelectedEntitySize, selectedBody, surfaceRaycastHit.Position);
                
                        var physicsCenter = new float3(startPointOverride.x + offset.x, surfaceRaycastHit.Position.y + offset.y, startPointOverride.z + offset.z);
                        modifiedSelectedEntityTransform.Position = physicsCenter - centerOffsetRelativeToRot;
                        overlapAabbInput.Aabb = AABBComponent.Create(physicsCenter, selectedEntityGeometry.Size);
                        resultIndexlist.Clear();
                        if ( physicsWorld.OverlapAabb(overlapAabbInput, ref resultIndexlist) )
                        {
                            var hasCollision = false;
                            for ( var index = 0; index < resultIndexlist.Length && !hasCollision; index++ )
                            {
                                int resultIndex = resultIndexlist[index];
                                var body = physicsWorld.Bodies[resultIndex];
                                hasCollision = body.Entity != buildInputData.SelectedEntity;
                            }
                            isPlacementObstructed = hasCollision;
                        }
                    }
                }
            }
            if ( math.distancesq(modifiedSelectedEntityTransform.Position, selectedEntityTransform.Position) > _MINIMUM_DISTANCE_FOR_POS_CHANGE_SQ )
            {
                state.EntityManager.SetComponentData(buildInputData.SelectedEntity, modifiedSelectedEntityTransform);
            }

            buildInputData.CanBePlaced = foundSurface
                                         && foundValidPlacementSurface && !isPlacementObstructed
                                         && filterMaskHitCollector.HasFoundSurfaceWithCorrectAngle;
        }

        private static void SetDefaultGridSnappingValue(ref GridRuntimeData gridRuntimeData, ref GridSettingsData gridSettingsData)
        {
            Assert.IsTrue(gridSettingsData.SnapGridCellSizes.Length > 0, "GridSettingsData.SnapGridCellSizes must have at least one value");
            gridRuntimeData.CurrentSnapIndex = math.clamp(gridRuntimeData.CurrentSnapIndex, 0, gridSettingsData.SnapGridCellSizes.Length);
            gridRuntimeData.CurrentSnapping.Value = gridSettingsData.SnapGridCellSizes[gridRuntimeData.CurrentSnapIndex].Value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 GetCenterPointInWorldSpace(in RigidBody selectedBody, out BoxGeometry scaledGeometry)
        {
            scaledGeometry = GetScaledBoxGeometry(ref selectedBody.Collider.Value, selectedBody.Scale);
            return selectedBody.WorldFromBody.pos + math.mul(selectedBody.WorldFromBody.rot, scaledGeometry.Center);
        }

        private static float3 GetSnappingOffset(PlacementRelativeTo snappingPreference, float3 contactGeometrySize, float3 halfSelectedEntitySize, RigidBody selectedBody, float3 surfaceHitPosition)
        {
            var offset = float3.zero;
            if ( snappingPreference == PlacementRelativeTo.Top )
            {
                offset.y = contactGeometrySize.y + halfSelectedEntitySize.y;
            }
            else if ( snappingPreference == PlacementRelativeTo.Front )
            {
                offset.z = contactGeometrySize.z + halfSelectedEntitySize.z;
            }
            else if ( snappingPreference == PlacementRelativeTo.Left )
            {
                offset.x = -halfSelectedEntitySize.x;
            }
            else if ( snappingPreference == PlacementRelativeTo.Right )
            {
                offset.x = contactGeometrySize.x + halfSelectedEntitySize.x;
            }
            else if ( snappingPreference == PlacementRelativeTo.Horizontal )
            {
                offset.x = surfaceHitPosition.x > selectedBody.WorldFromBody.pos.x
                    ? contactGeometrySize.x + halfSelectedEntitySize.x
                    : -halfSelectedEntitySize.x;
            }
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static BoxGeometry GetBoxGeometry(ref Collider collider)
        {
            return ((BoxCollider*)UnsafeUtility.AddressOf(ref collider))->Geometry;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static BoxGeometry GetScaledBoxGeometry(ref Collider collider, float uniformScale)
        {
            BoxGeometry scaledBoxGeometry = ((BoxCollider*)UnsafeUtility.AddressOf(ref collider))->Geometry;
            ApplyUniformScaleToGeometry(ref scaledBoxGeometry, uniformScale);
            return scaledBoxGeometry;
        }

        private static void SetGridSnapValuesFromSelectedObject(ref GridRuntimeData gridData, BoxGeometry selectedEntityGeometry, ref GridSettingsData gridSettingsData)
        {
            var snapGridCellSize = gridSettingsData.SnapGridCellSizes[gridData.CurrentSnapIndex].Value;
            gridData.CurrentSnapping.Value.x = math.min(selectedEntityGeometry.Size.x + (selectedEntityGeometry.BevelRadius * 2), snapGridCellSize.x);
            gridData.CurrentSnapping.Value.z = math.min(selectedEntityGeometry.Size.z + (selectedEntityGeometry.BevelRadius * 2), snapGridCellSize.y);
            gridData.CurrentSnapping.Value.y = selectedEntityGeometry.Size.y;
        }

        private static void ApplyUniformScaleToGeometry(ref BoxGeometry boxColliderGeometry, float uniformScale)
        {
            boxColliderGeometry.Center *= uniformScale;
            boxColliderGeometry.Size *= uniformScale;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryQuerySelectablePhysicsEntity(PhysicsWorldSingleton physicsWorld, RaycastInput raycastInput, NativeList<RaycastHit> castResults, in BuildInputData buildInputData, ref Entity foundEntity)
        {
            return physicsWorld.CastRay(raycastInput, ref castResults)
                   && FilterCollisionResultEntity(ref castResults, buildInputData.SelectedEntity, out foundEntity);
        }

        private static bool FilterCollisionResultEntity(ref NativeList<RaycastHit> overlapIndexResults, Entity collisionRoot, out Entity entity)
        {
            entity = default;
            var nonSelfEntityFound = false;
            for ( int i = 0; i < overlapIndexResults.Length && !nonSelfEntityFound; i++ )
            {
                entity = overlapIndexResults[i].Entity;
                nonSelfEntityFound = entity != collisionRoot;
            }
            return nonSelfEntityFound;
        }

        public void OnStopRunning(ref SystemState state)
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static RaycastInput CreateRaycastInputFromRay(Ray ray, float maxAllowedRayDistance)
        {
            return new RaycastInput {
                Start = ray.origin,
                End = ray.origin + ray.direction * maxAllowedRayDistance,
                Filter = new CollisionFilter {
                    BelongsTo = _EVERYTHING_MASK
                }
            };
        }

        /// <summary>   A collector which stores only the closest hit with preference for certain surfaces and angles. </summary>
        public struct FilterSurfaceRaycastHitCollector : ICollector<RaycastHit>
        {
            [ReadOnly] public NativeArray<RigidBody> Bodies;
            [ReadOnly] private NativeArray<float3> _allowedSurfaceAngles;

            public bool HasFoundSurfaceWithCorrectAngle {
                get;
                private set;
            }
            private readonly float _angleDotThreshold;
            public uint PreviousHitFilter { get; private set; }
            public uint SearchHitFilter { get; private set; }
            /// <summary>   Gets a value indicating whether the early out on first hit. </summary>
            ///
            /// <value> False. </value>
            public bool EarlyOutOnFirstHit => false;
            public Entity Ignore { get; private set; }

            /// <summary>   Gets or sets the maximum fraction. </summary>
            ///
            /// <value> The maximum fraction. </value>
            public float MaxFraction { get; private set; }

            /// <summary>   Gets  the number of hits. </summary>
            ///
            /// <value> The total number of hits (0 or 1). </value>
            public int NumHits => PreviousHitFilter.ContainsBitMask(SearchHitFilter) ? 1 : 0;

            private RaycastHit m_ClosestHit;

            /// <summary>   Gets the closest hit. </summary>
            ///
            /// <value> The closest hit. </value>
            public RaycastHit ClosestHit => m_ClosestHit;

            public FilterSurfaceRaycastHitCollector(NativeArray<float3> allowedSurfaceAngles, NativeArray<RigidBody> bodies, uint searchHitFilter, Entity ignore = default, float surfaceDotAngleThreshold = _CLOSE_IN_DIRECTION_DOT_PRODUCT_THRESHOLD_THRESHOLD, float maxFraction = 1f)
            {
                _allowedSurfaceAngles = allowedSurfaceAngles;
                SearchHitFilter = searchHitFilter;
                Ignore = ignore;
                PreviousHitFilter = 0;
                Bodies = bodies;
                MaxFraction = maxFraction;
                m_ClosestHit = default;
                HasFoundSurfaceWithCorrectAngle = false;
                _angleDotThreshold = surfaceDotAngleThreshold;
            }

            public bool HasCorrectSurfaceAngle(float3 hitSurfaceNormal)
            {
                for ( var index = 0; index < _allowedSurfaceAngles.Length; index++ )
                {
                    float3 allowedSurfaceAngle = _allowedSurfaceAngles[index];
                    var dotProduct = math.dot(allowedSurfaceAngle, hitSurfaceNormal);
                    if ( dotProduct >= _angleDotThreshold )
                    {
                        return true;
                    }
                }
                return _allowedSurfaceAngles.Length == 0;
            }

            #region ICollector
            /// <summary>   Adds a hit. </summary>
            ///
            /// <param name="hit">  The hit. </param>
            ///
            /// <returns>   True. </returns>
            public bool AddHit(RaycastHit hit)
            {
                RigidBody rigidBody = Bodies.AsReadOnlySpan()[hit.RigidBodyIndex];
                var hitBelongTo = rigidBody.Collider.Value.GetCollisionFilter().BelongsTo;
                bool searchedSurfaceNotFound = !PreviousHitFilter.ContainsBitMask(SearchHitFilter);
                bool containsSearchMask = hitBelongTo.ContainsBitMask(SearchHitFilter);
                bool hasCorrectSurfaceAngle = false;

                if ( Ignore != hit.Entity
                     && (containsSearchMask && (hasCorrectSurfaceAngle = HasCorrectSurfaceAngle(hit.SurfaceNormal)))
                     || (searchedSurfaceNotFound && containsSearchMask)
                     || ((hit.Fraction <= MaxFraction && searchedSurfaceNotFound))
                   )
                {
                    HasFoundSurfaceWithCorrectAngle = hasCorrectSurfaceAngle;
                    PreviousHitFilter = hitBelongTo;
                    MaxFraction = hit.Fraction;
                    m_ClosestHit = hit;
                    return true;
                }
                return false;
            }
            #endregion
        }

        public struct FilterMaskHitCollector<T> : ICollector<T>
            where T : struct, IQueryResult
        {
            public NativeArray<RigidBody> Bodies { get; private set; }
            public uint PreviousHitFilter { get; private set; }
            public uint SearchHitFilter { get; private set; }
            /// <summary>   Gets a value indicating whether the early out on first hit. </summary>
            ///
            /// <value> False. </value>
            public bool EarlyOutOnFirstHit => false;
            public Entity Ignore { get; private set; }

            /// <summary>   Gets or sets the maximum fraction. </summary>
            ///
            /// <value> The maximum fraction. </value>
            public float MaxFraction { get; private set; }

            /// <summary>   Gets  the number of hits. </summary>
            ///
            /// <value> The total number of hits (0 or 1). </value>
            public int NumHits => PreviousHitFilter.ContainsBitMask(SearchHitFilter) ? 1 : 0;

            private T m_ClosestHit;

            /// <summary>   Gets the closest hit. </summary>
            ///
            /// <value> The closest hit. </value>
            public T ClosestHit => m_ClosestHit;

            public FilterMaskHitCollector(NativeArray<RigidBody> bodies, uint searchHitFilter, Entity ignore = default, float maxFraction = 1f)
            {
                SearchHitFilter = searchHitFilter;
                Ignore = ignore;
                PreviousHitFilter = 0;
                Bodies = bodies;
                MaxFraction = maxFraction;
                m_ClosestHit = default;
            }

            #region ICollector
            /// <summary>   Adds a hit. </summary>
            ///
            /// <param name="hit">  The hit. </param>
            ///
            /// <returns>   True. </returns>
            public bool AddHit(T hit)
            {
                var hitBelongTo = Bodies.AsReadOnlySpan()[hit.RigidBodyIndex].Collider.Value.GetCollisionFilter().BelongsTo;
                if ( Ignore != hit.Entity
                     && ((hitBelongTo.ContainsBitMask(SearchHitFilter))
                         || ((hit.Fraction <= MaxFraction && !PreviousHitFilter.ContainsBitMask(SearchHitFilter))))
                   )
                {
                    PreviousHitFilter = hitBelongTo;
                    MaxFraction = hit.Fraction;
                    m_ClosestHit = hit;
                    return true;
                }
                return false;
            }
            #endregion
        }
    }
}