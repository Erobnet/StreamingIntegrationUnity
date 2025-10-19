using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Extensions;

namespace ChatBot.Runtime
{
    /// <summary>
    /// update and apply text value received from the chat enabling the tag to notify other systems
    /// </summary>
    [UpdateInGroup(typeof(ChatBotSystemGroup))]
    [UpdateAfter(typeof(ChatMessageProvidersSystemGroup))]
    public partial struct ApplyChatUserTextFromChatSystem : ISystem, ISystemStartStop
    {
        private ChatSystemRuntimeData _chatSystemRuntimeData;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ChatSystemRuntimeData>();
        }

        public void OnStartRunning(ref SystemState state)
        {
            _chatSystemRuntimeData = SystemAPI.GetSingleton<ChatSystemRuntimeData>();
        }

        [BurstCompile]
        void ISystem.OnUpdate(ref SystemState state)
        {
            //reset the old hasnewtexttag enabled to false after a frame
            EntityQuery resetUpdateNewTextTagQuery = SystemAPI.QueryBuilder()
                .WithAllRW<HasNewTextTag>()
                .Build();
            state.EntityManager.SetComponentEnabled<HasNewTextTag>(resetUpdateNewTextTagQuery, false);
            ApplyChatUserText(ref state, ref _chatSystemRuntimeData);
            DisposeChatUserEntitiesResources(ref state);
        }

        private void ApplyChatUserText(ref SystemState state, ref ChatSystemRuntimeData chatSystemRuntimeData)
        {
            for ( int i = 0; i < chatSystemRuntimeData.ChatUserCharacterDisplayTextBuffer.Length; i++ )
            {
                var applyTextRequest = chatSystemRuntimeData.ChatUserCharacterDisplayTextBuffer[i];
                var chatUserEntity = applyTextRequest.ChatUserRoot;

                if ( !state.EntityManager.TrySetComponentEnabled<HasNewTextTag>(chatUserEntity, true) ) // the entity have been destroyed ignore it
                    continue;

                ref var chatUserText = ref state.EntityManager.GetComponentDataRW<ChatUserText>(chatUserEntity).ValueRW;
                chatUserText.Text.Clear();
                chatUserText.Text.Append(applyTextRequest.text);
            }
            chatSystemRuntimeData.ChatUserCharacterDisplayTextBuffer.Clear();
        }

        /// <summary>
        /// deallocate <see cref="ChatUserText"/> native memory of destroyed entities
        /// </summary>
        private void DisposeChatUserEntitiesResources(ref SystemState state)
        {
            var allDestroyedChatUserQuery = SystemAPI.QueryBuilder()
                .WithAll<ChatUserText>()
                .WithAbsent<HasNewTextTag>()
                .Build();

            var userTexts = allDestroyedChatUserQuery.ToComponentDataArray<ChatUserText>(Allocator.Temp);
            for ( var index = 0; index < userTexts.Length; index++ )
            {
                var chatUserText = userTexts[index];
                chatUserText.Text.Dispose();
            }
            var allDestroyedChatUserEntities = allDestroyedChatUserQuery.ToEntityArray(Allocator.Temp);
            state.EntityManager.RemoveComponent<ChatUserText>(allDestroyedChatUserQuery);
            state.EntityManager.DestroyEntity(allDestroyedChatUserEntities);
        }

        public void OnStopRunning(ref SystemState state)
        { }

        public void OnDestroy(ref SystemState state)
        {
            var allDestroyedChatUserQuery = SystemAPI.QueryBuilder()
                .WithAll<HasNewTextTag, ChatUserText>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                .Build();
            state.EntityManager.DestroyEntity(allDestroyedChatUserQuery.ToEntityArray(Allocator.Temp));
            DisposeChatUserEntitiesResources(ref state);
        }
    }
}