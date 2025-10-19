using System.Runtime.CompilerServices;
using ChatBot.Runtime;
using Drboum.Utilities.Entities;
using GameProject.Animation;
using GameProject.Characters;
using GameProject.ChatApp;
using GameProject.GameWorldData;
using GameProject.Persistence.CommonData;
using ProjectDawn.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Extensions;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace GameProject.ArticifialIntelligence
{
    internal partial struct AgentDecisionExecutionSystem : ISystem
    {
        private Unity.Mathematics.Random _random;
        private EntityQuery _mainPlayingFieldAreaQuery;

        public void OnCreate(ref SystemState state)
        {
            _random = new Unity.Mathematics.Random(420);
            _mainPlayingFieldAreaQuery = SystemAPI.QueryBuilder()
                .WithAll<NavigationAreaData, MainTerrainAreaDataTag>()
                .Build();
            state.RequireForUpdate<GlobalAgentSettingsData>();
            state.RequireForUpdate<ChatSystemRuntimeData>();
            state.RequireForUpdate(_mainPlayingFieldAreaQuery);
        }

        [BurstCompile]
        unsafe void ISystem.OnUpdate(ref SystemState state)
        {
            var agentSettingsData = SystemAPI.GetSingleton<GlobalAgentSettingsData>();
            ProcessAgentBrainStates(ref state, agentSettingsData);
            ProcessShopsLineCustomerPositions(ref state);
            HandleShopsProducingAndCustomerSale(ref state);
        }

        private void ProcessAgentBrainStates(ref SystemState state, in GlobalAgentSettingsData agentSettingsData)
        {
            foreach ( var (agentBrainStateComponentRW, characterHierarchyHubData, timerRW, grabItemRequest, agentEntity)
                     in SystemAPI.Query<RefRW<AgentBrainStateComponent>, CharacterHierarchyHubData, RefRW<TimerComponent>, GrabItemRequest>().WithEntityAccess() )
            {
                ref var timer = ref timerRW.ValueRW;
                var agentBrainStateComponentCopy = agentBrainStateComponentRW.ValueRO;
                bool hasChangedState = agentBrainStateComponentCopy.HasChanged;
                switch ( agentBrainStateComponentCopy.AgentBrainState )
                {
                    case AgentBrainState.Idle:
                        if ( hasChangedState )
                        {
                            timer.SetTimer(state.WorldUnmanaged.Time.ElapsedTime, agentSettingsData.IdleDuration);
                        }
                        if ( timer.TimeIsUp(state.WorldUnmanaged.Time.ElapsedTime) )
                        {
                            agentBrainStateComponentCopy.AgentBrainState = AgentBrainState.MoveToNextRandomDestination;
                        }
                        break;

                    case AgentBrainState.MoveToNextRandomDestination:
                        if ( hasChangedState )
                        {
                            SetRandomMoveDestination(ref state, characterHierarchyHubData, ref _random);
                            timer.SetTimer(state.WorldUnmanaged.Time.ElapsedTime, agentSettingsData.WaitTimeBeforeNewDestination);
                            break;
                        }

                        if ( AgentHasArrived(ref state, characterHierarchyHubData.MovementRoot) )
                        {
                            agentBrainStateComponentCopy.AgentBrainState = AgentBrainState.Idle;
                        }
                        else if ( timer.TimeIsUp(state.WorldUnmanaged.Time.ElapsedTime) )
                        {
                            agentBrainStateComponentRW.ValueRW.AgentBrainState = AgentBrainState.None; //make it to reenter the haschanged
                        }
                        break;

                    case AgentBrainState.GoToPartingLocation:
                        if ( hasChangedState )
                        {
                            var spawnPointQuery = SystemAPI.QueryBuilder().WithAll<SpawnPoint>().Build();
                            var spawnPoints = spawnPointQuery.ToComponentDataArray<SpawnPoint>(Allocator.Temp);
                            var leavePoint = ExecuteChatBotCommandSystem.GetSpawnPoint(spawnPoints);
                            SetMoveDestination(ref state, leavePoint.Position, characterHierarchyHubData.MovementRoot);
                            timer.SetTimer(state.WorldUnmanaged.Time.ElapsedTime, agentSettingsData.TimeOutToGoOutWhenLeavingGameInSeconds);
                        }
                        else if ( AgentHasArrived(ref state, characterHierarchyHubData.MovementRoot) || timer.TimeIsUp(state.WorldUnmanaged.Time.ElapsedTime) )
                        {
                            agentBrainStateComponentCopy.AgentBrainState = AgentBrainState.CharacterReadyToLeaveGame;
                        }
                        break;

                    case AgentBrainState.RequestCoffee:
                        var shopQuery = SystemAPI.QueryBuilder().WithAll<CoffeeShopData>().Build();
                        var coffeeShopDatas = shopQuery.ToComponentDataArray<CoffeeShopData>(Allocator.Temp);
                        var coffeeShopEntities = shopQuery.ToEntityArray(Allocator.Temp);
                        if ( hasChangedState )
                        {
                            if ( coffeeShopDatas.Length != 0 )
                            {
                                //add ourselves in the shop queue
                                //we use the first one for now
                                Entity coffeeShopEntity = coffeeShopEntities[0];
                                var shopQueue = state.EntityManager.GetBuffer<QueueLine>(coffeeShopEntity);
                                var isAlreadyInQueue = false;
                                for ( var index = 0; index < shopQueue.Length && !isAlreadyInQueue; index++ )
                                {
                                    var waitingEntity = shopQueue[index];
                                    isAlreadyInQueue = waitingEntity.AgentEntity == agentEntity;
                                }

                                if ( !isAlreadyInQueue )
                                {
                                    shopQueue.Add(new QueueLine {
                                        AgentEntity = agentEntity,
                                    });
                                    state.EntityManager.SetComponentEnabled<QueueLine>(coffeeShopEntity, true);
                                }
                            }
                            else // handle the case where no shop is up 
                            {
                                //play shrug animation ?
                                agentBrainStateComponentCopy.AgentBrainState = AgentBrainState.Idle;
                            }
                        }
                        break;
                    case AgentBrainState.ObtainedCoffee:
                        if ( hasChangedState )
                        {
                            SetItemSkin(ref state,in characterHierarchyHubData,in grabItemRequest);
                            SetRandomMoveDestination(ref state, characterHierarchyHubData, ref _random);
                        }
                        else if ( AgentHasArrived(ref state, characterHierarchyHubData.MovementRoot) )
                        {
                            agentBrainStateComponentCopy.AgentBrainState = AgentBrainState.DrinkCoffeeAndChill;
                        }
                        break;
                }

                agentBrainStateComponentRW.ValueRW.AgentBrainState = agentBrainStateComponentCopy.AgentBrainState;
            }
        }

        private unsafe void ProcessShopsLineCustomerPositions(ref SystemState state)
        {
            var mainPlayFieldAreaData = _mainPlayingFieldAreaQuery.GetSingleton<NavigationAreaData>();

            var forbiddenNavigationAreas = SystemAPI.QueryBuilder()
                .WithAll<ForbiddenNavigationAreaData>()
                .Build().ToComponentDataArray<ForbiddenNavigationAreaData>(Allocator.Temp);

            const float characterSpacing = 1.5f; //for now
            var boxOffsetToAvoidBorderOverlap = .05f;
            var searchPositionsDirections = stackalloc float3[4] {
                math.right(),
                math.left(),
                math.forward(),
                math.back()
            };
            float centerOffset = characterSpacing / 2f;

            foreach ( var (queueLine, coffeeShopData, shopTransform, enabledQueueLineRef)
                     in SystemAPI.Query<DynamicBuffer<QueueLine>, CoffeeShopData, LocalToWorld, EnabledRefRW<QueueLine>>() )
            {
                if ( queueLine.IsEmpty )
                    continue;

                var queueLineAsArray = queueLine.AsNativeArray();
                float3 initialoffset = GetFirstInLinePosition(shopTransform, coffeeShopData, out float3 shopFirstInLinePosition);
                float3 slotPosition = shopFirstInLinePosition;
                queueLine.ElementAt(0).AgentReservedSpace = AABBComponent.Create(slotPosition, characterSpacing - boxOffsetToAvoidBorderOverlap);
                SetAgentPositionDestination(ref state, slotPosition, queueLine[0].AgentEntity);
                for ( var index = 1; index < queueLineAsArray.Length; index++ )
                {
                    ref var waitingInLine = ref queueLineAsArray.ReadElementAsRef(index);
                    var attemptIndex = 0;
                    float3 newPosInLine;
                    bool foundPoint;
                    do
                    {
                        newPosInLine = slotPosition + CalculatePositionOffset(searchPositionsDirections[attemptIndex], characterSpacing);
                        float3 positionAtCharacterCenter = newPosInLine + centerOffset;
                        waitingInLine.AgentReservedSpace = AABBComponent.Create(positionAtCharacterCenter, characterSpacing - boxOffsetToAvoidBorderOverlap);

                        foundPoint = mainPlayFieldAreaData.Value.Contains(newPosInLine);

                        for ( int i = 0; i < forbiddenNavigationAreas.Length && foundPoint; i++ )
                        {
                            var forbiddenNavigationArea = forbiddenNavigationAreas[i];
                            foundPoint = !forbiddenNavigationArea.Bounds.Value.Contains(waitingInLine.AgentReservedSpace);
                        }

                        for ( var checkIndex = index - 1; checkIndex >= 1 && foundPoint; checkIndex-- )
                        {
                            var checkOverlapQueueLine = queueLineAsArray[checkIndex];
                            foundPoint = !checkOverlapQueueLine.AgentReservedSpace.Contains(waitingInLine.AgentReservedSpace);
                        }
                        attemptIndex++;
                    }
                    while ( !foundPoint && attemptIndex < 4 );

                    slotPosition = newPosInLine;
                    SetAgentPositionDestination(ref state, slotPosition, waitingInLine.AgentEntity);
                }
                enabledQueueLineRef.ValueRW = false;
            }
        }

        private void HandleShopsProducingAndCustomerSale(ref SystemState state)
        {
            foreach ( var (queueLine, coffeeShopDataRW, transform, enabledQueueLineRefRW)
                     in SystemAPI.Query<DynamicBuffer<QueueLine>, RefRW<CoffeeShopData>, LocalToWorld, EnabledRefRW<QueueLine>>()
                         .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState) )
            {
                ref var coffeeShopData = ref coffeeShopDataRW.ValueRW;
                GetFirstInLinePosition(transform, coffeeShopData, out float3 shopFirstInLinePosition);
                if ( queueLine.IsEmpty )
                    continue;

                var firstInLineEntity = queueLine.ElementAt(0).AgentEntity;
                var firstInLineChatCurrency = SystemAPI.GetComponent<GameCurrency>(firstInLineEntity);
                var firstInLineRequestedItem = SystemAPI.GetComponent<GrabItemRequest>(firstInLineEntity).ItemAssetDataReference;
                Entity movementRoot = SystemAPI.GetComponent<CharacterHierarchyHubData>(firstInLineEntity).MovementRoot;
                float3 positionOfCustomer = SystemAPI.GetComponent<LocalTransform>(movementRoot).Position;
                var validServingArea = AABBComponent.Create(shopFirstInLinePosition, coffeeShopData.ServingAreaSize);

                //initiate coffee making for customer
                bool preparingCoffeeForCustomer = coffeeShopData.CurrentSpentTimeOnCustomer > 0;
                if ( preparingCoffeeForCustomer )
                {
                    coffeeShopData.CurrentSpentTimeOnCustomer += SystemAPI.Time.DeltaTime;
                }
                else if ( validServingArea.Contains(positionOfCustomer) )
                {
                    coffeeShopData.CurrentSpentTimeOnCustomer = SystemAPI.Time.DeltaTime;
                }

                bool doesNotHaveEnoughMoney = firstInLineChatCurrency < firstInLineRequestedItem.PurchaseCost;
                if ( coffeeShopData.CurrentSpentTimeOnCustomer >= coffeeShopData.CoffeeServingDuration
                     || doesNotHaveEnoughMoney
                     || !state.EntityManager.Exists(firstInLineEntity)
                   )
                {
                    //serve prepared coffee
                    if ( doesNotHaveEnoughMoney )
                    {
                        // had no money send him somewhere else
                        SystemAPI.GetComponentRW<AgentBrainStateComponent>(firstInLineEntity).ValueRW.AgentBrainState = AgentBrainState.MoveToNextRandomDestination;
                    }
                    else
                    {
                        state.EntityManager.SetComponentData(firstInLineEntity, firstInLineChatCurrency - firstInLineRequestedItem.PurchaseCost);
                        // set holding coffee state 
                        SystemAPI.GetComponentRW<AgentBrainStateComponent>(firstInLineEntity).ValueRW.AgentBrainState = AgentBrainState.ObtainedCoffee;
                    }
                    // remove customer 
                    queueLine.RemoveAt(0);
                    coffeeShopData.CurrentSpentTimeOnCustomer = 0;
                    enabledQueueLineRefRW.ValueRW = true;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetAgentPositionDestination(ref SystemState state, float3 position, Entity agentEntity)
        {
            Entity movementRoot = state.EntityManager.GetComponentData<CharacterHierarchyHubData>(agentEntity).MovementRoot;
            SetMoveDestination(ref state, position, movementRoot);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetItemSkin(ref SystemState state, in CharacterHierarchyHubData characterHierarchyHubData, in GrabItemRequest grabItemRequest)
        {
            var applyCoffeeAnims = state.EntityManager.GetBuffer<SkinOptionOverrideApply>(characterHierarchyHubData.AnimationRoot);
            applyCoffeeAnims.Add(new SkinOptionOverrideApply {
                CharacterTransitionWrapper = grabItemRequest.CharacterTransitionData,
                SelectedColor = grabItemRequest.ColorOption
            });
            state.EntityManager.SetComponentEnabled<SkinOptionOverrideApply>(characterHierarchyHubData.AnimationRoot, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 CalculatePositionOffset(quaternion offset, float characterSpacing)
        {
            return math.forward(offset) * characterSpacing;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 CalculatePositionOffset(float3 direction, float characterSpacing)
        {
            return direction * characterSpacing;
        }

        private void SetRandomMoveDestination(ref SystemState state, CharacterHierarchyHubData characterHierarchyHubData, ref Random random)
        {
            var navigationAreaData = _mainPlayingFieldAreaQuery.GetSingleton<NavigationAreaData>();
            var forbiddenNavAreas = SystemAPI.QueryBuilder()
                .WithAll<ForbiddenNavigationAreaData>()
                .Build()
                .ToComponentDataArray<ForbiddenNavigationAreaData>(Allocator.Temp);
            float3 destinationAttempt;
            bool isValidPosition;
            do
            {
                isValidPosition = true;
                destinationAttempt = random.NextFloat3(navigationAreaData.Value.Min, navigationAreaData.Value.Max);

                for ( var index = 0; index < forbiddenNavAreas.Length && isValidPosition; index++ )
                {
                    var forbiddenNavArea = forbiddenNavAreas[index];
                    isValidPosition &= !forbiddenNavArea.Bounds.Value.Contains(destinationAttempt);
                }
            }
            while ( !isValidPosition );
            SetMoveDestination(ref state, destinationAttempt, characterHierarchyHubData.MovementRoot);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 GetFirstInLinePosition(LocalToWorld shopTransform, CoffeeShopData coffeeShopData, out float3 shopFirstInLinePosition)
        {
            float3 initialoffset = math.mul(shopTransform.Rotation, coffeeShopData.StartQueuePositionOffset);
            shopFirstInLinePosition = shopTransform.Position + initialoffset;
            return initialoffset;
        }

        private static bool AgentHasArrived(ref SystemState state, Entity movementRoot)
        {
            var agentBody = state.EntityManager.GetComponentData<AgentBody>(movementRoot);
            return agentBody.HasArrived() && !state.EntityManager.IsComponentEnabled<MoveToDestinationData>(movementRoot);
        }

        private static void SetMoveDestination(ref SystemState state, float3 destinationAttempt, Entity movementRoot)
        {
            ref var moveToDestinationData = ref state.EntityManager.GetComponentDataRW<MoveToDestinationData>(movementRoot).ValueRW;
            moveToDestinationData.SetDestination(state.EntityManager, movementRoot, destinationAttempt);
        }
    }

    public struct AgentBrainStateComponent : IComponentData
    {
        private UIntState _uIntState;
        public bool HasChanged => _uIntState.HasChanged;

        public AgentBrainState AgentBrainState {
            get => (AgentBrainState)_uIntState.CurrentState;
            set => _uIntState.CurrentState = (uint)value;
        }
    }

    public enum AgentBrainState : byte
    {
        None,
        Idle,
        MoveToNextRandomDestination,
        RequestCoffee,
        ObtainedCoffee,
        DrinkCoffeeAndChill,
        GoToPartingLocation,
        CharacterReadyToLeaveGame
    }
}