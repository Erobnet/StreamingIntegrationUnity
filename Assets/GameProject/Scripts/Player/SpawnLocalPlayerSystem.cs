using System;
using GameProject.Animation;
using GameProject.Characters;
using ChatBot.Runtime;
using GameProject.ChatApp;
using GameProject.Persistence;
using ProjectDawn.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Extensions;
using Unity.Transforms;
using ProcessCharacterAgentSpawnSystem = GameProject.Characters.ProcessCharacterAgentSpawnSystem;
using Random = Unity.Mathematics.Random;

namespace GameProject.Player
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(ChatBotSystemGroup))]
    public partial struct SpawnLocalPlayerSystem : ISystem, ISystemStartStop
    {
        private Random _random;
        private EntityQuery _playerPrefabQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PersistenceSystem.PersistenceCachedData>();
            state.RequireForUpdate<ChatSystemRuntimeData>();
            state.RequireForUpdate<CharacterCreationComponentData>();
            state.RequireForUpdate<PlayerUserScreenNameData>();
            var entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp);
            _playerPrefabQuery = entityQueryBuilder
                .WithPlayerRoot()
                .WithAll<Prefab>()
                .WithOptions(EntityQueryOptions.IncludePrefab)
                .Build(ref state);
            state.RequireForUpdate(_playerPrefabQuery);
            _random.InitState((uint)DateTime.Now.Ticks);
        }

        [BurstCompile]
        public unsafe void OnStartRunning(ref SystemState state)
        {
            var userScreenNameData = SystemAPI.GetSingleton<PlayerUserScreenNameData>();
            var chatSystemRuntimeData = SystemAPI.GetSingleton<ChatSystemRuntimeData>();
            var persistenceCachedData = SystemAPI.GetSingleton<PersistenceSystem.PersistenceCachedData>();

            var hostUserComponent = new ChatUserComponent {
                UserId = ChatUser.AsHostUser(),
                UserScreenName = userScreenNameData.UserScreenName
            };
            BufferLookup<PersistentSkinOptionApply> skinOptionIndexLookup = SystemAPI.GetBufferLookup<PersistentSkinOptionApply>();
            BufferLookup<PersistentSkinColorOptionApply> skinColorOptionLookup = SystemAPI.GetBufferLookup<PersistentSkinColorOptionApply>();
            var playerPrefab = _playerPrefabQuery.GetSingletonEntity();
            var playerEntities = new NativeArray<Entity>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            ExecuteChatBotCommandSystem.InstantiateNewChatUserRootInstances(playerPrefab, state.EntityManager, playerEntities);

            var playerEntity = playerEntities[0];
            ExecuteChatBotCommandSystem.InitializeChatUserRootInstance(in playerEntity, state.EntityManager, in hostUserComponent, Allocator.Persistent);
            chatSystemRuntimeData.IngamePlayerLookup[hostUserComponent.UserId] = playerEntity;
            for ( var index = 0; index < chatSystemRuntimeData.HostIdentities.Length; index++ )
            {
                var hostIdentity = chatSystemRuntimeData.HostIdentities[index];
                chatSystemRuntimeData.IngamePlayerLookup[hostIdentity] = playerEntity;
            }

            skinOptionIndexLookup.Update(ref state);
            skinColorOptionLookup.Update(ref state);
            var hierarchyHub = state.EntityManager.GetComponentData<CharacterHierarchyHubData>(playerEntity);
            if ( SystemAPI.TryGetSingleton(out PersistenceSystem.PlayerPersistentWorldData playerPersistentData) )
            {
                ref var localTransform = ref state.EntityManager.GetComponentDataRW<LocalTransform>(hierarchyHub.MovementRoot).ValueRW;
                localTransform.Position = playerPersistentData.Position;
                localTransform.Rotation = playerPersistentData.Rotation;
            }
            if ( persistenceCachedData.CharacterDataLookup.TryGetValue(in hostUserComponent.UserId, out var chatCurrency, out var skinOptions, out var skinColorOptions) )
            {
                state.EntityManager.SetComponentData(playerEntity, chatCurrency);
                ProcessCharacterAgentSpawnSystem.ApplyOptionIndices(hierarchyHub.AnimationRoot, ref skinOptions.Indices, skinOptionIndexLookup);
                ProcessCharacterAgentSpawnSystem.ApplyOptionIndices(hierarchyHub.AnimationRoot, ref skinColorOptions.Indices, skinColorOptionLookup);
            }
        }

        [BurstCompile]
        public void OnStopRunning(ref SystemState state)
        { }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        { }
    }

    public static partial class EntityQueryExtensions
    {
        public static EntityQueryBuilder WithPlayerRoot(this ref EntityQueryBuilder entityQueryBuilder) => entityQueryBuilder.WithAll<CharacterHierarchyHubData, LocalPlayerTag>();
        public static EntityQueryBuilder WithMovementRoot(this ref EntityQueryBuilder entityQueryBuilder) => entityQueryBuilder.WithAll<LocalTransform, CharacterGameplayStateComponent, LocalPlayerTag>();
    }
}