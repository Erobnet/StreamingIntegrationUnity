using System;
using System.Runtime.CompilerServices;
using Drboum.Utilities.Runtime;
using GameProject.ArticifialIntelligence;
using GameProject.CameraManagement;
using GameProject.GameWorldData;
using ProjectDawn.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;

namespace GameProject.Characters
{
    [RequireMatchingQueriesForUpdate]
    public partial struct GameCharacterGameplaySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        { }

        public struct TargetRequest
        {
            public Entity Target;
            public float3 CurrentPosition;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var mainCameraTransform = SystemAPI.QueryBuilder()
                .WithAll<MainCameraTag, PositionRotationData>()
                .Build()
                .GetSingleton<PositionRotationData>();
            var cameraForward = math.forward(mainCameraTransform.Rotation);

            new ProcessCharacterGameplayUpdateJob() {
                TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                SittableLookup = SystemAPI.GetComponentLookup<SittableData>(true),
                VacancyLookup = SystemAPI.GetComponentLookup<HasVacancy>(),
                MoveToDestLookup = SystemAPI.GetComponentLookup<MoveToDestinationData>(),
                NavMeshPathLookup = SystemAPI.GetComponentLookup<NavMeshPath>(),
                CameraForward = cameraForward.FlattenNormalize(),
                LastSystemVersion = state.LastSystemVersion
            }.Schedule();
        }

        [BurstCompile]
        public partial struct ProcessCharacterGameplayUpdateJob : IJobEntity
        {
            private static readonly ProfilerMarker _ExecuteMarker = new(nameof(ProcessCharacterGameplayUpdateJob) + "Marker");

            [ReadOnly, NativeDisableContainerSafetyRestriction] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<SittableData> SittableLookup;
            public ComponentLookup<HasVacancy> VacancyLookup;
            public ComponentLookup<MoveToDestinationData> MoveToDestLookup;
            public ComponentLookup<NavMeshPath> NavMeshPathLookup;
            public float3 CameraForward;
            public uint LastSystemVersion;

            void Execute(Entity entity, ref CharacterGameplayStateComponent characterGameplayStateComponent, in TargetEntity targetEntity, ref LocalTransform transform, in AgentBody agentBody, in GameCharacterPartOffsets characterPartOffsets)
            {
                _ExecuteMarker.Begin();
                var oldState = characterGameplayStateComponent;
                var newCharacterState = oldState;
#if UNITY_EDITOR
                var requireVacancy = CharacterGameplayState.Sitting | CharacterGameplayState.RequestingSit;
                if ( oldState.Is(requireVacancy) && !VacancyLookup.HasComponent(targetEntity.Target, out bool entityExists) && entityExists )
                {
                    Debug.LogError($"the entity ({targetEntity.Target}) does not have the {(FixedString128Bytes)nameof(HasVacancy)} component assigned.");
                }
#endif
                if ( oldState.HasEnterState(CharacterGameplayState.RequestingSit) )
                {
                    SetSitAsDestination(targetEntity, entity, in characterPartOffsets);
                }
                else if ( oldState.Is(CharacterGameplayState.RequestingSit) )
                {
                    if ( TransformLookup.DidChange(targetEntity.Target, LastSystemVersion) )
                    {
                        SetSitAsDestination(targetEntity, entity, in characterPartOffsets);
                    }
                    if ( agentBody.HasArrived() )
                    {
                        if ( VacancyLookup.IsComponentEnabled(targetEntity.Target) )
                        {
                            newCharacterState.CurrentState = CharacterGameplayState.Sitting;
                            ref var hasVacancyRW = ref VacancyLookup.GetRefRW(targetEntity.Target).ValueRW;
                            hasVacancyRW.Entity = entity;
                            VacancyLookup.SetComponentEnabled(targetEntity.Target, false);
                        }
                    }
                }

                if ( oldState.HasEnterState(CharacterGameplayState.Sitting) )
                {
                    NavMeshPathLookup.SetComponentEnabled(entity, false);
                    transform.Position = GetSitPosition(targetEntity, in characterPartOffsets);
                }
                else if ( oldState.HasExitState(CharacterGameplayState.Sitting) )
                {
                    NavMeshPathLookup.SetComponentEnabled(entity, true);
                    bool isOwner = VacancyLookup.GetRefRO(targetEntity.Target).ValueRO.Entity == entity;
                    bool doesNotHaveVacancy = !VacancyLookup.IsComponentEnabled(targetEntity.Target);
                    if ( doesNotHaveVacancy
                         && isOwner )
                    {
                        VacancyLookup.SetComponentEnabled(targetEntity.Target, true);
                    }
                }

                characterGameplayStateComponent.CurrentState = newCharacterState.CurrentState;

                _ExecuteMarker.End();
            }

            private void SetSitAsDestination(in TargetEntity targetEntity, Entity entity, in GameCharacterPartOffsets characterPartOffsets, float zOffset = .01f)
            {
                float3 pointToSit = GetSitPosition(targetEntity, in characterPartOffsets, zOffset);
                MoveToDestinationData moveToDestinationData = MoveToDestLookup[entity];
                moveToDestinationData.SetDestination(MoveToDestLookup, entity, pointToSit);
                MoveToDestLookup[entity] = moveToDestinationData;
            }

            private float3 GetSitPosition(in TargetEntity targetEntity, in GameCharacterPartOffsets characterPartOffsets, float zOffset = .01f)
            {
                var sittableData = SittableLookup[targetEntity.Target];
                var targetTransform = TransformLookup[targetEntity.Target];
                return (targetTransform.Position + math.mul(targetTransform.Rotation, sittableData.SitPointOffset) + characterPartOffsets.ButtPointOffset) - (CameraForward * zOffset);
            }
        }
    }

    [Flags]
    public enum CharacterGameplayState : uint
    {
        None = 0,
        MovingToSelectedDestination = 1 << 1,
        RequestingSit = 1 << 2,
        Sitting = 1 << 3,
        Drinking = 1 << 4,
        HoldingItem = 1 << 5,
        Serving = 1 << 6,
    }

}