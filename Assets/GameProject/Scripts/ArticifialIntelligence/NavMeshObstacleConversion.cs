using Drboum.Utilities.Entities;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Hybrid;
using UnityEngine;
using UnityEngine.AI;
using static Unity.Entities.SystemAPI;

namespace GameProject
{
    class NavMeshObstacleConverterBaker : Baker<NavMeshObstacle>
    {
        public override void Bake(NavMeshObstacle authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            var additionalEntity = CreateAdditionalEntity(TransformUsageFlags.None, entityName: authoring.name + "_NavObstacle");
            AddComponent<NavMeshObstacleData>(additionalEntity, authoring);
            AddComponent(additionalEntity, new EntityReference {
                Value = entity
            });
        }
    }

    public struct NavMeshObstacleData : IComponentData
    {
        public float height;
        public float radius;
        public Vector3 velocity;
        public bool carving;
        public bool carveOnlyStationary;
        public float carvingMoveThreshold;
        public float carvingTimeToStationary;
        public NavMeshObstacleShape shape;
        public Vector3 center;
        public Vector3 size;

        public static implicit operator NavMeshObstacleData(NavMeshObstacle authoring)
        {
            var scale = authoring.transform.lossyScale;
            return new NavMeshObstacleData {
                height = authoring.height * scale.y,
                shape = authoring.shape,
                size = Multiply(scale, authoring.size),
                center = Multiply(scale, authoring.center),
                carveOnlyStationary = authoring.carveOnlyStationary,
                carvingMoveThreshold = authoring.carvingMoveThreshold,
                velocity = authoring.velocity,
                carvingTimeToStationary = authoring.carvingTimeToStationary,
                radius = authoring.radius * scale.x,
                carving = authoring.carving,
            };
        }

        private static Vector3 Multiply(Vector3 scale, Vector3 relativeVector)
        {
            return new Vector3(relativeVector.x * scale.x, relativeVector.y * scale.y, relativeVector.z * scale.z);
        }
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public unsafe partial class NavMeshObstacleCompanionSystem : SystemBase
    {
        private NavMeshObstacle _navMeshObstacleCompanionPrototype;

        private NavMeshObstacle InstantiateNavMeshObstacle()
        {
            return GameObject.Instantiate(_navMeshObstacleCompanionPrototype);
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            if ( !_navMeshObstacleCompanionPrototype )
            {
                var prototypeGameObject = new GameObject(nameof(NavMeshObstacleCompanionSystem) + "_Instance", typeof(NavMeshObstacle));
                GameObject.DontDestroyOnLoad(prototypeGameObject);

                _navMeshObstacleCompanionPrototype = prototypeGameObject.GetComponent<NavMeshObstacle>();
                _navMeshObstacleCompanionPrototype.size = Vector3.zero;
                _navMeshObstacleCompanionPrototype.gameObject.SetActive(false);
            }
        }

        protected override void OnUpdate()
        {
            EntityManager entityManager = EntityManager;

            EntityQuery navMeshObstacleCreationQuery = QueryBuilder()
                .WithAll<Simulate, NavMeshObstacleData, EntityReference>()
                .Build();

            if ( !navMeshObstacleCreationQuery.IsEmpty )
            {
                var transformSourceEntities = new NativeList<Entity>(128, Allocator.Temp);
                var transformSources = new NativeList<UnityObjectRef<Transform>>(128, Allocator.Temp);
                var gameobjectTransformSources = new NativeList<UnityObjectRef<GameObject>>(128, Allocator.Temp);
                foreach ( var (navMeshObstacleData, transformEntitySource)
                         in Query<NavMeshObstacleData, EntityReference>() )
                {
                    var obstacle = InstantiateNavMeshObstacle();
                    obstacle.height = navMeshObstacleData.height;
                    obstacle.radius = navMeshObstacleData.radius;
                    obstacle.velocity = navMeshObstacleData.velocity;
                    obstacle.carving = navMeshObstacleData.carving;
                    obstacle.carveOnlyStationary = navMeshObstacleData.carveOnlyStationary;
                    obstacle.carvingMoveThreshold = navMeshObstacleData.carvingMoveThreshold;
                    obstacle.carvingTimeToStationary = navMeshObstacleData.carvingTimeToStationary;
                    obstacle.shape = navMeshObstacleData.shape;
                    obstacle.center = navMeshObstacleData.center;
                    obstacle.size = navMeshObstacleData.size;
                    GameObject obstacleGameObject = obstacle.gameObject;
                    obstacleGameObject.SetActive(true);
                    transformSourceEntities.Add(in transformEntitySource.Value);
                    transformSources.Add(obstacleGameObject.transform);
                    gameobjectTransformSources.Add(obstacleGameObject);
                }

                entityManager.DestroyEntity(navMeshObstacleCreationQuery);
                entityManager.AddTransformCompanionComponent(transformSourceEntities.AsArray(), transformSources.AsArray(), gameobjectTransformSources.AsArray());
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if ( _navMeshObstacleCompanionPrototype )
                _navMeshObstacleCompanionPrototype.gameObject.Destroy();
        }
    }
}