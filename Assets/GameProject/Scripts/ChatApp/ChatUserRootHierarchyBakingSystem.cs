using ChatBot.Runtime;
using GameProject.Characters;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace GameProject.ChatApp
{
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateInGroup(typeof(PostBakingSystemGroup))]
    internal partial struct ChatUserRootHierarchyBakingSystem : ISystem
    {
        public unsafe void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HasBakedCharacterViewerPrefab>();
        }

        [BurstCompile]
        unsafe void ISystem.OnUpdate(ref SystemState state)
        {
            if ( !SystemAPI.TryGetSingletonRW<ChatBotGameAppSettings>(out var chatBotGameAppSettingsRW) || !SystemAPI.TryGetSingleton<ChatBotUserRootBakingSettings>(out var chatBotGameAppBakingSettings) )
                return;

            ref var chatBotGameAppSettings = ref chatBotGameAppSettingsRW.ValueRW;
            if ( !state.EntityManager.HasComponent<CharacterHierarchyHubData>(chatBotGameAppBakingSettings.ChatUserCharacterPrefab) )
            {
                Debug.LogError($"No required {(FixedString128Bytes)(nameof(CharacterHierarchyHubData))} on entity {chatBotGameAppBakingSettings.ChatUserCharacterPrefab}");
                return;
            }
            
            var characterHierarchyHubData = state.EntityManager.GetComponentData<CharacterHierarchyHubData>(chatBotGameAppBakingSettings.ChatUserCharacterPrefab);
            state.EntityManager.SetComponentData(chatBotGameAppSettings.ChatUserRootPrefab, characterHierarchyHubData);
        }
    }
}