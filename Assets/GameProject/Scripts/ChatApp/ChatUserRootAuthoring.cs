using System;
using Drboum.Utilities.Entities;
using GameProject.ArticifialIntelligence;
using GameProject.Characters;
using GameProject.ItemManagement;
using GameProject.Persistence.CommonData;
using ProjectDawn.Navigation.Hybrid;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GameProject.ChatApp
{
    public class ChatUserRootAuthoring : MonoBehaviour
    {
        protected class Baker : Baker<ChatUserRootAuthoring>
        {
            public override void Bake(ChatUserRootAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.None);
                //required by the chat systems
                this.BakeChatBotUserRootRequiredComponents(entity);
                // Components specific to this project
                AddComponent<CharacterHierarchyHubData>(entity);
                AddComponent<ActiveUserData>(entity);
                AddComponent<GameCurrency>(entity);

                // features and properties of the chat user
                //AI part
                AddComponent<AgentBrainStateComponent>(entity);
                AddComponent<TimerComponent>(entity);
                //required for a character to grab things
                AddComponent<GrabItemRequest>(entity);
            }
        }
    }

    public class AgentAuthoringBaker : Baker<AgentAuthoring>
    {
        public override void Bake(AgentAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent<MoveToDestinationData>(entity);
            AddComponent(entity, new RequestRotation {
                DesiredRotation = quaternion.identity
            });
            AddComponent<InitializeEntityTag>(entity);
            SetComponentEnabled<MoveToDestinationData>(entity, false);
            SetComponentEnabled<RequestRotation>(entity, false);
        }
    }

    public struct GrabItemRequest : IComponentData
    {
        public ItemAssetDataReference ItemAssetDataReference;
        public UnityObjectRef<CharacterTransitionData> CharacterTransitionData;
        public UnityObjectRef<Material> ColorOption;
    }
}