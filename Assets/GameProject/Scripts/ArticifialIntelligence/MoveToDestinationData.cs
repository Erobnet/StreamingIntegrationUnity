using Drboum.Utilities.Entities;
using ProjectDawn.Navigation;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Properties;
using Unity.Transforms;

namespace GameProject.ArticifialIntelligence
{
    public struct MoveToDestinationData : IComponentData, IEnableableComponent
    {
        [CreateProperty] private float3 _destination;

        public float3 Destination => _destination;

        public void SetDestination(EntityManager entityManager, Entity entity, in float3 destination)
        {
            _destination = destination;
            entityManager.SetComponentEnabled<MoveToDestinationData>(entity, true);
        }

        public void SetDestination(ComponentLookup<MoveToDestinationData> moveToLookup, Entity entity, in float3 destination)
        {
            _destination = destination;
            moveToLookup.SetComponentEnabled(entity, true);
        }
    }

    public struct RequestRotation : IComponentData, IEnableableComponent
    {
        public quaternion DesiredRotation;
    }

    [UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
    internal partial struct MoveToDestinationInitializeSystem : ISystem
    {
        [BurstCompile]
        void ISystem.OnUpdate(ref SystemState state)
        {
            EntityQuery initializeMoveToQuery = SystemAPI.QueryBuilder()
                .WithAll<MoveToDestinationData, InitializeEntityTag>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                .Build();

            if ( !initializeMoveToQuery.IsEmpty )
            {
                state.EntityManager.SetComponentEnabled<MoveToDestinationData>(initializeMoveToQuery, false);
            }

            var requestRotationQuery = SystemAPI.QueryBuilder()
                .WithAll<RequestRotation, InitializeEntityTag>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                .Build();
            if ( !requestRotationQuery.IsEmpty )
            {
                state.EntityManager.SetComponentEnabled<RequestRotation>(requestRotationQuery, false);
            }
        }
    }

    public partial struct MoveToDestinationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new SetAgentBodyDestinationJob().Schedule();
            new RotateOnArrivalJob().Schedule();
        }

        [BurstCompile]
        public partial struct SetAgentBodyDestinationJob : IJobEntity
        {
            private static readonly ProfilerMarker _ExecuteMarker = new(nameof(SetAgentBodyDestinationJob) + "Marker");

            public void Execute(ref AgentBody agentBody, ref MoveToDestinationData moveToDestinationData, EnabledRefRW<MoveToDestinationData> enabledDestination)
            {
                _ExecuteMarker.Begin();
                agentBody.SetDestination(moveToDestinationData.Destination);
                enabledDestination.ValueRW = false;
                _ExecuteMarker.End();
            }
        }

        [BurstCompile]
        public partial struct RotateOnArrivalJob : IJobEntity
        {
            private static readonly ProfilerMarker _ExecuteMarker = new(nameof(RotateOnArrivalJob) + "Marker");

            public void Execute(ref LocalTransform localTransform, ref RequestRotation requestRotation, in AgentBody agent, EnabledRefRW<RequestRotation> requestRotationRW)
            {
                _ExecuteMarker.Begin();
                if ( agent.HasArrived() )
                {
                    localTransform.Rotation = requestRotation.DesiredRotation;
                    requestRotationRW.ValueRW = false;
                }
                _ExecuteMarker.End();
            }
        }
    }
}