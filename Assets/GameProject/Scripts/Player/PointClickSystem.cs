using GameProject.ArticifialIntelligence;
using GameProject.CameraManagement;
using GameProject.Characters;
using GameProject.GameWorldData;
using GameProject.Inputs;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Extensions;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using Ray = UnityEngine.Ray;
using RaycastHit = Unity.Physics.RaycastHit;

namespace GameProject.Player
{
    [BurstCompile]
    [UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
    public partial class PointClickSystem : SystemBase
    {
        private const uint _PHYSICS_MASK_EVERYTHING = ~0u;
        private float2 _cursorPosition;
        private bool _hasClicked = false;
        private PointClickComponentData _pointClickComponentData;
        private Entity _pointClickDataEntity;
        private EntityQuery _playerRootQuery;
        private InputSystemCoordinator _coordinator;
        private GameObject _clickRenderingGameObject;

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<PointClickComponentData>();
            var entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp);
            _playerRootQuery = entityQueryBuilder
                .WithPlayerRoot()
                .Build(ref CheckedStateRef);
            RequireForUpdate(_playerRootQuery);
        }

        protected override void OnStartRunning()
        {
            _coordinator = InputSystemCoordinator.AddOnClickEvent(World, OnClickPerformed);
            _pointClickDataEntity = SystemAPI.GetSingletonEntity<PointClickComponentData>();
            _pointClickComponentData = EntityManager.GetComponentData<PointClickComponentData>(_pointClickDataEntity);

            if ( !_clickRenderingGameObject )
                _clickRenderingGameObject = GameObject.Instantiate(_pointClickComponentData.TargetMarkerPrefab.Value);

            _clickRenderingGameObject.SetActive(false);
        }

        private bool OnClickPerformed(in InputSystemCoordinator.ClickInputContext inputActionCallback)
        {
            _cursorPosition = inputActionCallback.ClickPosition;
            _hasClicked = !inputActionCallback.HasClickedOnUI;
            return _hasClicked;
        }

        protected override void OnUpdate()
        {
#if UNITY_EDITOR
            _pointClickComponentData = EntityManager.GetComponentData<PointClickComponentData>(_pointClickDataEntity);
#endif
            var playerHierarchies = _playerRootQuery.ToComponentDataArray<CharacterHierarchyHubData>(Allocator.Temp);
            var hierarchyHubData = playerHierarchies[0];
            if ( _hasClicked )
            {
                var ray = SystemAPI.QueryBuilder().WithAll<RayData, MainCameraTag>().Build().GetSingleton<RayData>().Value;
                var physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
                EntityManager entityManager = EntityManager;
                EntityQueryMask sittableQueryMask = SystemAPI.QueryBuilder().WithAll<SittableData>().Build().GetEntityQueryMask();
                if ( TryComputeClick(ref entityManager, in ray, in physicsWorldSingleton, in _pointClickDataEntity, in _cursorPosition, _pointClickComponentData.GroundLayer | _pointClickComponentData.ActionnableObjectMask, in sittableQueryMask, in hierarchyHubData, out RaycastHit raycastHit) )
                {
                    SetMarkerAtPosition(raycastHit.Position + (raycastHit.SurfaceNormal * _pointClickComponentData.MarkerOffsetFromGround), _clickRenderingGameObject);
                }
                _hasClicked = false;
            }
            else
            {
                GameObject gameObject = _clickRenderingGameObject;
                if ( gameObject && gameObject.activeSelf )
                {
                    float3 playerPosition = EntityManager.GetComponentData<LocalTransform>(hierarchyHubData.MovementRoot).Position;
                    float distancesq = math.distancesq(playerPosition, gameObject.transform.position);
                    var hasNotArrived = distancesq > _pointClickComponentData.MarkerDisapearDistanceSq;
                    gameObject.SetActive(hasNotArrived);
                }
            }
        }

        [BurstCompile]
        private static unsafe bool TryComputeClick(ref EntityManager entityManager, in Ray ray, in PhysicsWorldSingleton physicsWorldSingleton, in Entity pointClickDataEntity, in float2 cursorPosition, uint surfaceMask, in EntityQueryMask sittableQueryMask, in CharacterHierarchyHubData playerEntities, out RaycastHit resultHit)
        {
            resultHit = default;

            var raycastInput = new RaycastInput {
                Start = ray.origin,
                End = ray.origin + ray.direction * 100,
                Filter = new CollisionFilter {
                    BelongsTo = _PHYSICS_MASK_EVERYTHING,
                    CollidesWith = surfaceMask | surfaceMask
                }
            };
            var list = new NativeList<RaycastHit>(Allocator.Temp);
            if ( physicsWorldSingleton.CastRay(raycastInput, ref list) )
            {
                resultHit = list[0];
                bool isSittable = false;
                for ( int i = 0; i < list.Length; i++ )
                {
                    var currentHit = list[i];
                    if ( sittableQueryMask.MatchesIgnoreFilter(currentHit.Entity) )
                    {
                        isSittable = true;
                        resultHit = currentHit;
                        break;
                    }
                }

                if ( isSittable )
                {
                    entityManager.GetComponentDataRW<TargetEntity>(playerEntities.MovementRoot).ValueRW.Target = resultHit.Entity;
                    entityManager.GetComponentDataRW<CharacterGameplayStateComponent>(playerEntities.MovementRoot).ValueRW.CurrentState = CharacterGameplayState.RequestingSit;
                }
                else
                {
                    ref var playerMoveToData = ref entityManager.GetComponentDataRW<MoveToDestinationData>(playerEntities.MovementRoot).ValueRW;
                    playerMoveToData.SetDestination(entityManager, playerEntities.MovementRoot, resultHit.Position);
                    entityManager.GetComponentDataRW<CharacterGameplayStateComponent>(playerEntities.MovementRoot).ValueRW.CurrentState = CharacterGameplayState.MovingToSelectedDestination;
                }
                return true;
            }
            return false;
        }

        private void SetMarkerAtPosition(float3 clickPosition, GameObject pointClickComponentData)
        {
            if ( !pointClickComponentData )
                return;

            if ( !pointClickComponentData.activeSelf )
            {
                pointClickComponentData.SetActive(true);
            }
            pointClickComponentData.transform.position = clickPosition;
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            _coordinator?.RemoveOnClickEvent(OnClickPerformed);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if ( _clickRenderingGameObject )
                _clickRenderingGameObject.Destroy();
        }

    }
}