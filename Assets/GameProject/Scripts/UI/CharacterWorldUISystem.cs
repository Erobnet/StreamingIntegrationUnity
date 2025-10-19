using GameProject.Characters;
using ChatBot.Runtime;
using GameProject.ChatApp;
using GameProject.Persistence.CommonData;
using GameProject.Player;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;
using static Unity.Entities.SystemAPI;

namespace GameProject.UI
{
    [UpdateInGroup(typeof(ChatBotSystemGroup))]
    [UpdateAfter(typeof(ExecuteChatBotCommandSystem))]
    public partial class CharacterWorldUISystem : SystemBase
    {
        private CharacterWorldUIGlobalCanvas _characterWorldUIGlobalCanvasCanvas;
        private ChatBubbleGameObjectPrefab _bubbleControllerPrototype;
        private ObjectPool<WorldCharacterUI> _chatBubblePool;

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<ChatBubbleGameObjectPrefab>();
            _chatBubblePool = new ObjectPool<WorldCharacterUI>(CreateChatBubbleController, GetFromPool, ReleaseFromPool);
        }

        private WorldCharacterUI CreateChatBubbleController()
        {
            var chatBubbleController = Object.Instantiate(_bubbleControllerPrototype.Prefab.Value.Component, _characterWorldUIGlobalCanvasCanvas.transform);
            chatBubbleController.gameObject.SetActive(false);
            return chatBubbleController;
        }

        private void GetFromPool(WorldCharacterUI worldCharacterUI)
        {
            worldCharacterUI.gameObject.SetActive(true);
        }

        private void ReleaseFromPool(WorldCharacterUI worldCharacterUI)
        {
            worldCharacterUI.gameObject.SetActive(false);
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            _bubbleControllerPrototype = SystemAPI.GetSingleton<ChatBubbleGameObjectPrefab>();
        }

        protected override void OnUpdate()
        {
            if ( !_characterWorldUIGlobalCanvasCanvas )
            {
                _characterWorldUIGlobalCanvasCanvas = SceneManager.GetActiveScene().FindFirstInstancesInScene<CharacterWorldUIGlobalCanvas>();
            }

            if ( !_characterWorldUIGlobalCanvasCanvas )
                return;

#if UNITY_EDITOR
            _bubbleControllerPrototype = SystemAPI.GetSingleton<ChatBubbleGameObjectPrefab>();
#endif
            var requireInitChatUserUIQuery = QueryBuilder()
                .WithPresent<HasNewTextTag>()
                .WithNone<CharacterWorldUIComponentRef>()
                .Build();

            var newChatUserEntities = requireInitChatUserUIQuery.ToEntityArray(Allocator.Temp);
            EntityManager.AddComponent<CharacterWorldUIComponentRef>(requireInitChatUserUIQuery);
            for ( var index = 0; index < newChatUserEntities.Length; index++ )
            {
                var chatUserEntity = newChatUserEntities[index];
                var chatBubbleController = _chatBubblePool.Get();
                EntityManager.SetComponentData(chatUserEntity, new CharacterWorldUIComponentRef {
                    Ref = chatBubbleController,
                });
                chatBubbleController.SetCharacterName(EntityManager.GetComponentData<ChatUserComponent>(chatUserEntity).UserScreenName.Value);
                if ( EntityManager.HasComponent<LocalPlayerTag>(chatUserEntity) )
                {
                    chatBubbleController.SetUIForStreamer();
                }
                else
                {
                    chatBubbleController.SetUIForNonStreamer();
                }
            }

            foreach ( var (chatUserText, hasNewTextTag, chatBubbleComponentRef)
                     in Query<ChatUserText, EnabledRefRW<HasNewTextTag>, CharacterWorldUIComponentRef>()
                         .WithAll<HasNewTextTag>()
                         .WithChangeFilter<ChatUserText>() )
            {
                chatBubbleComponentRef.Ref.Value.ChatDisplayText = chatUserText.Text.Value;
                hasNewTextTag.ValueRW = false;
            }

            var requireDisposalChatUserUIQuery = QueryBuilder()
                .WithAll<CharacterWorldUIComponentRef>()
                .WithAbsent<HasNewTextTag>()
                .Build();

            var disposeChatUsers = requireDisposalChatUserUIQuery.ToComponentDataArray<CharacterWorldUIComponentRef>(Allocator.Temp);
            for ( var index = 0; index < disposeChatUsers.Length; index++ )
            {
                var chatBubbleComponentRef = disposeChatUsers[index];
                _chatBubblePool.Release(chatBubbleComponentRef.Ref.Value);
            }

            var disposeEntities = requireDisposalChatUserUIQuery.ToEntityArray(Allocator.Temp);
            EntityManager.RemoveComponent<CharacterWorldUIComponentRef>(requireDisposalChatUserUIQuery);
            EntityManager.DestroyEntity(disposeEntities);

            var hasHierarchyChanges = newChatUserEntities.Length != 0 || disposeEntities.Length != 0;
            if ( hasHierarchyChanges )
                _characterWorldUIGlobalCanvasCanvas.UpdateHierarchy();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _chatBubblePool.Clear();
            _chatBubblePool.Dispose();
        }
    }

    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    internal partial class CharacterWorldUILateUpdateSystem : SystemBase
    {
        private ChatBubbleGameObjectPrefab _bubbleControllerPrototype;

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<ChatBubbleGameObjectPrefab>();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            _bubbleControllerPrototype = SystemAPI.GetSingleton<ChatBubbleGameObjectPrefab>();
        }

        protected override void OnUpdate()
        {
#if UNITY_EDITOR
            _bubbleControllerPrototype = SystemAPI.GetSingleton<ChatBubbleGameObjectPrefab>();
#endif
            foreach ( var (characterHierarchyHubData, chatBubbleComponentRef)
                     in Query<CharacterHierarchyHubData, CharacterWorldUIComponentRef>() )
            {
                var localTransform = EntityManager.GetComponentData<LocalTransform>(characterHierarchyHubData.MovementRoot);
                float3 bubblePosition = localTransform.Position + _bubbleControllerPrototype.ChatBubblePositionOffset;
                chatBubbleComponentRef.Ref.Value.UpdatePosition(bubblePosition);
            }

            foreach ( var (chatCurrency, characterWorldUIComponentRef)
                     in Query<GameCurrency, CharacterWorldUIComponentRef>()
                         .WithAbsent<LocalPlayerTag>()
                         .WithChangeFilter<GameCurrency>() )
            {
                characterWorldUIComponentRef.Ref.Value.SetCharacterCurrencyDisplay(in chatCurrency);
            }
        }
    }
    
    public struct CharacterWorldUIComponentRef : ICleanupComponentData
    {
        public UnityObjectRef<WorldCharacterUI> Ref;
    }
}