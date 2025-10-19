using System.Collections.Generic;
using System.Diagnostics;
using Drboum.Utilities.Runtime;
using GameProject.Animation;
using JetBrains.Annotations;
using ProjectDawn.Navigation.Hybrid;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;
using UnityEngine;

namespace GameProject.Characters
{
    public class GameCharacterAuthoring : MonoBehaviour
    {
        public CharacterAnimationRoot characterAnimator;
        public AgentAuthoring characterRoot;
        public Transform ButtPoint;

        private class Baker : Baker<GameCharacterAuthoring>
        {
            public override void Bake(GameCharacterAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new CharacterHierarchyHubData {
                    AnimationRoot = GetEntity(authoring.characterAnimator, TransformUsageFlags.Dynamic),
                    MovementRoot = GetEntity(authoring.characterRoot, TransformUsageFlags.Dynamic)
                });
                AddComponent(entity, new GameCharacterPartOffsets {
                    ButtPointOffset = authoring.transform.position - authoring.ButtPoint.position,
                });
                AddComponent<CharacterGameplayStateComponent>(entity);
                AddComponent<TargetEntity>(entity);
            }
        }
    }

    [DebuggerDisplay("{ToString()}")]
    public struct CharacterGameplayStateComponent : IComponentData, IState<CharacterGameplayState>
    {
        private UIntState _state;

        [Pure]
        public bool Is(CharacterGameplayState state)
        {
            return _state.CurrentState.ContainsBitMask((uint)state);
        }

        public void ReenterState(CharacterGameplayState defaultState = default)
        {
            _state.ReenterState((uint)defaultState);
        }

        public bool HasChanged => _state.HasChanged;

        public bool HasEnterState(CharacterGameplayState state)
        {
            return _state.HasEnterState((uint)state);
        }

        public bool HasExitState(CharacterGameplayState state)
        {
            return _state.HasExitState((uint)state);
        }

        [CreateProperty]
        public CharacterGameplayState CurrentState {
            get => (CharacterGameplayState)_state.CurrentState;
            set => _state.CurrentState = (uint)value;
        }

        public override string ToString()
        {
            return $"current state= {(CharacterGameplayState)_state.CurrentState}, PreviousState= {(CharacterGameplayState)_state.PreviousState}";
        }
    }

    public struct TargetEntity : IComponentData
    {
        public Entity Target;
    }

    public struct GameCharacterPartOffsets : IComponentData
    {
        public float3 ButtPointOffset;
    }

    public struct CharacterHierarchyHubData : IComponentData
    {
        public Entity AnimationRoot;
        public Entity MovementRoot;
    }
}