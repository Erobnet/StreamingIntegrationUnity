using System;
using System.Runtime.CompilerServices;
using ChatBot.Runtime;
using GameProject.Animation;
using GameProject.ArticifialIntelligence;
using GameProject.Characters;
using GameProject.Persistence;
using GameProject.Persistence.CommonData;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Entities.Extensions;
using ProcessCharacterAgentSpawnSystem = GameProject.Characters.ProcessCharacterAgentSpawnSystem;
using Random = Unity.Mathematics.Random;

namespace GameProject.ChatApp
{
    [UpdateInGroup(typeof(ChatBotSystemGroup))]
    [UpdateAfter(typeof(ChatMessageProvidersSystemGroup))]
    [UpdateBefore(typeof(ApplyChatUserTextFromChatSystem))]
    internal partial struct ExecuteChatBotCommandSystem : ISystem, ISystemStartStop
    {
        private Random _systemRandom;
        private ChatSystemRuntimeData _chatSystemRuntimeData;
        private ChatBotGameAppSettings _chatBotGameAppSettings;
        private ChatBotInactivitySettings _chatBotChatBotInactivitySettings;
        private EntityQuery _characterCreationDataQuery;
        private NativeList<Entity> _playerEntitiesSystemBuffer;
        private NativeList<CharacterOptionIndex> _characterSwapSkinOptionApplyTemp;
        private PersistenceSystem.PersistenceCachedData _persistenceCachedData;
        private CharacterCreationComponentData _characterCreationComponentData;

        // [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _playerEntitiesSystemBuffer = new(100, Allocator.Persistent);
            _characterSwapSkinOptionApplyTemp = new(20, Allocator.Persistent);
            _characterCreationDataQuery = SystemAPI.QueryBuilder().WithAll<CharacterCreationComponentData>().Build();

            state.RequireForUpdate<SpawnPoint>();
            state.RequireForUpdate<ChatSystemRuntimeData>();
            state.RequireForUpdate<PersistenceSystem.PersistenceCachedData>();
            state.RequireForUpdate(_characterCreationDataQuery);
            state.RequireForUpdate<ChatBotGameAppSettings>();
            state.RequireForUpdate<ChatBotInactivitySettings>();
            _systemRandom.InitState((uint)DateTime.Now.Ticks.GetHashCode()); //can not burst DateTime struct
        }

        [BurstCompile]
        public void OnStartRunning(ref SystemState state)
        {
            _chatSystemRuntimeData = SystemAPI.GetSingleton<ChatSystemRuntimeData>();
            _persistenceCachedData = SystemAPI.GetSingleton<PersistenceSystem.PersistenceCachedData>();

            _characterCreationDataQuery.ResetFilter();
            _characterCreationComponentData = _characterCreationDataQuery.GetSingleton<CharacterCreationComponentData>();
            _characterCreationDataQuery.SetChangedVersionFilter<CharacterCreationComponentData>();
            _characterSwapSkinOptionApplyTemp.Length = _characterCreationComponentData.RuntimeSettings.Value.SkinOptionLenghts.Length;
            _chatBotGameAppSettings = SystemAPI.GetSingleton<ChatBotGameAppSettings>();
            _chatBotChatBotInactivitySettings = SystemAPI.GetSingleton<ChatBotInactivitySettings>();
        }

        [BurstCompile]
        void ISystem.OnUpdate(ref SystemState state)
        {
#if UNITY_EDITOR
            //those settings are not supposed to change at runtime hence the editor guard 
            {
                _chatBotGameAppSettings = SystemAPI.GetSingleton<ChatBotGameAppSettings>();
                _chatBotChatBotInactivitySettings = SystemAPI.GetSingleton<ChatBotInactivitySettings>();

                if ( !_characterCreationDataQuery.IsEmpty )
                {
                    _characterCreationComponentData = _characterCreationDataQuery.GetSingleton<CharacterCreationComponentData>();
                    _characterSwapSkinOptionApplyTemp.Length = _characterCreationComponentData.RuntimeSettings.Value.SkinOptionLenghts.Length;
                }
            }
#endif

            var characterViewerPrefab = _chatBotGameAppSettings.ChatUserRootPrefab;
            ProcessCharactersInstantiation(ref state, characterViewerPrefab);
            ExecuteChatCommands(ref state);
            foreach ( var (agentBrainStateComponent, chatUserComponent)
                     in SystemAPI.Query<AgentBrainStateComponent, ChatUserComponent>() )
            {
                if ( agentBrainStateComponent.AgentBrainState == AgentBrainState.CharacterReadyToLeaveGame )
                {
                    _chatSystemRuntimeData.AddPlayerToLeaveQueue(in chatUserComponent.UserId);
                }
            }
            ProcessCharacterLeaveQueue(ref state);
        }

        private void ExecuteChatCommands(ref SystemState state)
        {
            var characterSwapSkinLookup = SystemAPI.GetBufferLookup<PersistentSkinOptionApply>();
            var colorOptionIndexLookup = SystemAPI.GetBufferLookup<PersistentSkinColorOptionApply>();

            var entities = _chatSystemRuntimeData.PlayerCommandApplyQueue.AsKeysArray();
            var chatCommandInputs = _chatSystemRuntimeData.PlayerCommandApplyQueue.AsValuesArray();
            double timeElapsedTime = SystemAPI.Time.ElapsedTime;
            for ( var index = 0; index < entities.Length; index++ )
            {
                var entityKey = entities[index];
                var chatCommandInput = chatCommandInputs[index];
                switch ( (UserChatCommandType)chatCommandInput.TypeId.Value )
                {
                    case UserChatCommandType.RollCharacterSkinOption:
                        var charHub = state.EntityManager.GetComponentData<CharacterHierarchyHubData>(entityKey);
                        var optionIndicesTempArray = _characterSwapSkinOptionApplyTemp.AsArray();
                        ProcessCharacterAgentSpawnSystem.RollCharacterAppearance(optionIndicesTempArray, charHub, characterSwapSkinLookup, colorOptionIndexLookup, _characterCreationComponentData.RuntimeSettings, ref _systemRandom);
                        break;

                    case UserChatCommandType.OrderItem:
                        NativeArray<ChatCommandAvailableItem> chatCommandItems = default;
                        foreach ( var (chatCommandTypeComponent, chatCommandDataItems)
                                 in SystemAPI.Query<ChatCommandTypeComponent, DynamicBuffer<ChatCommandAvailableItem>>() )
                        {
                            if ( chatCommandInput.TypeId != chatCommandTypeComponent )
                                continue;

                            chatCommandItems = chatCommandDataItems.AsNativeArray();
                            break;
                        }

                        bool chatCommandWasFound = chatCommandItems.IsCreated;
                        if ( chatCommandWasFound )
                        {
                            GrabItemRequest? grabItemRequest = null;
                            if ( !chatCommandInput.ArgumentText.IsEmpty )
                            {
                                var argumentHashValue = HashValue.Create(chatCommandInput.ArgumentText);
                                for ( var i = 0; i < chatCommandItems.Length; i++ )
                                {
                                    var chatCommandAvailableItem = chatCommandItems[i];
                                    if ( chatCommandAvailableItem.ArgumentHash == argumentHashValue )
                                    {
                                        grabItemRequest = new GrabItemRequest {
                                            ItemAssetDataReference = chatCommandAvailableItem.ItemAssetDataReference,
                                            ColorOption = chatCommandAvailableItem.ColorOptions,
                                            CharacterTransitionData = chatCommandAvailableItem.CharacterTransitionData,
                                        };
                                        break;
                                    }
                                }
                            }

                            if ( grabItemRequest.HasValue )
                            {
                                state.EntityManager.GetComponentDataRW<AgentBrainStateComponent>(entityKey).ValueRW.AgentBrainState = AgentBrainState.RequestCoffee;
                                state.EntityManager.SetComponentData(entityKey, grabItemRequest.Value);
                            }
                            else
                            {
                                ChatUser chatUser = state.EntityManager.GetComponentData<ChatUserComponent>(entityKey).UserId;
                                if ( !_chatSystemRuntimeData.BotCanSendReplyToUser(chatUser) ) //user error handling send message to help resolve the situation
                                {
                                    var chatCommandInputText = _chatSystemRuntimeData.CommandDescriptionLookup[chatCommandInput.TypeId].ChatCommandInputText;
                                    var sendChatMessageCommand = new SendChatMessageCommand($"the argument '{chatCommandInput.ArgumentText}' is not valid for the command '{chatCommandInputText}' , valid arguments: ", chatCommandInput.OriginMessageID);
                                    for ( var i = 0; i < chatCommandItems.Length - 1; i++ )
                                    {
                                        var commandAvailableItem = chatCommandItems[i];
                                        AddRawText(ref sendChatMessageCommand, in commandAvailableItem);
                                        sendChatMessageCommand.MessageText.Append(' ');
                                        sendChatMessageCommand.MessageText.Append(',');
                                    }
                                    if ( chatCommandItems.Length > 0 )
                                    {
                                        var commandAvailableItem = chatCommandItems[^1];
                                        AddRawText(ref sendChatMessageCommand, in commandAvailableItem);
                                    }

                                    _chatSystemRuntimeData.SendReplyTo(in chatUser, in sendChatMessageCommand, timeElapsedTime);
                                }
                            }
                        }
                        break;

                    case UserChatCommandType.LeaveGame:
                        MakeCharacterLeaveGame(entityKey, state.EntityManager);
                        break;
                }
                TryUpdateUserActivity(state.EntityManager, entityKey, timeElapsedTime, _chatBotChatBotInactivitySettings.InactivityDelaySeconds);
            }
            _chatSystemRuntimeData.PlayerCommandApplyQueue.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddRawText(ref SendChatMessageCommand sendChatMessageCommand, in ChatCommandAvailableItem commandAvailableItem)
        {
            sendChatMessageCommand.MessageText.Append(in commandAvailableItem.ArgumentString.Value);
        }

        public static void MakeCharacterLeaveGame(Entity entity, EntityManager entityManager)
        {
            entityManager.GetComponentDataRW<AgentBrainStateComponent>(entity).ValueRW.AgentBrainState = AgentBrainState.GoToPartingLocation;
        }

        public static SpawnPoint GetSpawnPoint(NativeArray<SpawnPoint> spawnPoints)
        {
            return spawnPoints[0];
        }

        private void ProcessCharactersInstantiation(ref SystemState state, Entity chatUserRootPrefab)
        {
            using NativeArray<SpawnPoint> spawnPoints = SystemAPI.QueryBuilder()
                .WithAll<SpawnPoint>()
                .Build().ToComponentDataArray<SpawnPoint>(Allocator.Temp);

            SpawnPoint spawnPoint = GetSpawnPoint(spawnPoints);
            Entity movementRoot = state.EntityManager.GetComponentData<CharacterHierarchyHubData>(chatUserRootPrefab).MovementRoot;
            ref var spawnTransform = ref state.EntityManager.GetComponentDataRW<LocalTransform>(movementRoot).ValueRW;
            spawnTransform.Position = spawnPoint.Position;
            spawnTransform.Rotation = spawnPoint.SpawnRotation;
            EntityManager entityManager = state.EntityManager;
            if ( !_chatSystemRuntimeData.PlayerJoinQueue.IsEmpty()
                 && _chatSystemRuntimeData.IngamePlayerLookup.Count < _chatBotGameAppSettings.MaxSupportedViewerCharacterCount )
            {
                int spawnCount = math.min(_chatSystemRuntimeData.PlayerJoinQueue.Count, _chatBotGameAppSettings.MaxSupportedViewerCharacterCount - _chatSystemRuntimeData.IngamePlayerLookup.Count);
                _playerEntitiesSystemBuffer.Length = spawnCount;
                var outputEntities = _playerEntitiesSystemBuffer.AsArray();
                // update the prefab so all the instances will be updated as well
                TryUpdateUserActivity(entityManager, chatUserRootPrefab, SystemAPI.Time.ElapsedTime, _chatBotChatBotInactivitySettings.InactivityDelaySeconds);
                InstantiateNewChatUserRootInstances(chatUserRootPrefab, entityManager, outputEntities);
                for ( var joinIndex = 0; joinIndex < spawnCount; joinIndex++ )
                {
                    var joinChatUserInfo = _chatSystemRuntimeData.PlayerJoinQueue.Dequeue();
                    var chatUserInfo = joinChatUserInfo.ChatUserInfo;
                    Entity newViewerEntity = outputEntities[joinIndex];
                    InitializeChatUserRootInstance(in newViewerEntity, entityManager, in chatUserInfo, Allocator.Persistent);
                    _chatSystemRuntimeData.IngamePlayerLookup[chatUserInfo.UserId] = newViewerEntity;
                }
            }
        }

        public static void InstantiateNewChatUserRootInstances(Entity chatUserRootPrefab, EntityManager entityManager, NativeArray<Entity> outputEntities)
        {
            entityManager.Instantiate(chatUserRootPrefab, outputEntities);
            entityManager.AddComponent<ChatUserText>(outputEntities);
        }

        public static void InitializeChatUserRootInstance(in Entity newChatUserEntity, EntityManager entityManager, in ChatUserComponent chatUser, Allocator textAllocator)
        {
            entityManager.SetComponentData(newChatUserEntity, chatUser);
            entityManager.SetComponentData(newChatUserEntity, new ChatUserText {
                Text = new(64, textAllocator)
            });
        }

        private void ProcessCharacterLeaveQueue(ref SystemState state)
        {
            _playerEntitiesSystemBuffer.Clear();
            for ( var index = 0; index < _chatSystemRuntimeData.PlayerLeaveQueue.Length; index++ )
            {
                var chatUser = _chatSystemRuntimeData.PlayerLeaveQueue[index];
                if ( _chatSystemRuntimeData.HasGameCharacter(in chatUser, out var chatUserEntity) )
                {
                    _playerEntitiesSystemBuffer.Add(in chatUserEntity);

                    Entity animationRoot = state.EntityManager.GetComponentData<CharacterHierarchyHubData>(chatUserEntity).AnimationRoot;
                    var characterSwapSkinApplies = state.EntityManager.GetBuffer<PersistentSkinOptionApply>(animationRoot);
                    var characterColorApplies = state.EntityManager.GetBuffer<PersistentSkinColorOptionApply>(animationRoot);
                    var currency = state.EntityManager.GetComponentData<GameCurrency>(chatUserEntity);
                    var chatUserPropertiesPersistence = new ChatUserPropertiesPersistence {
                        Currency = currency
                    };
                    //doing it here before destroying the character
                    PersistenceSystem.WriteCharacterPersistentData(ref _persistenceCachedData, in chatUser, characterSwapSkinApplies.AsNativeArray(), characterColorApplies.AsNativeArray(), ref chatUserPropertiesPersistence);
                }
                _chatSystemRuntimeData.IngamePlayerLookup.Remove(chatUser);
            }
            state.EntityManager.DestroyEntity(_playerEntitiesSystemBuffer.AsArray());
            _chatSystemRuntimeData.PlayerLeaveQueue.Clear();
        }

        private static void TryUpdateUserActivity(EntityManager entityManager, Entity chatUserEntity, double currentElapsedTime, float inactivityDelay)
        {
            if ( !entityManager.TryGetComponentRW<ActiveUserData>(chatUserEntity, out var activeUserDataRW) )
                return;

            activeUserDataRW.ValueRW.ActiveUntil = (float)(currentElapsedTime + inactivityDelay);
        }

        [BurstCompile]
        public void OnStopRunning(ref SystemState state)
        {
            _characterCreationDataQuery.ResetFilter();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _characterSwapSkinOptionApplyTemp.Dispose();
            _playerEntitiesSystemBuffer.Dispose();
        }
    }
}