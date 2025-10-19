using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Extensions;
using UnityEngine;

namespace ChatBot.Runtime
{
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateInGroup(typeof(PostBakingSystemGroup))]
    internal partial struct ChatUserRootBakingSystem : ISystem
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
            if ( !state.EntityManager.HasComponent<Prefab>(chatBotGameAppBakingSettings.ChatUserCharacterPrefab) )
            {
                Debug.LogError($"{chatBotGameAppBakingSettings.ChatUserCharacterPrefab} must be a prefab");
                return;
            }

            if ( !state.EntityManager.HasComponent<Prefab>(chatBotGameAppSettings.ChatUserRootPrefab) )
            {
                Debug.LogError($"'{chatBotGameAppSettings.ChatUserRootPrefab}' must be a valid prefab entity.");
                return;
            }

            state.EntityManager.CreateNewLinkedGroupRootFrom(chatBotGameAppBakingSettings.ChatUserCharacterPrefab, chatBotGameAppSettings.ChatUserRootPrefab);
        }
    }
}