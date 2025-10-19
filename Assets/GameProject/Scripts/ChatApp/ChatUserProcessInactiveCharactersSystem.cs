using GameProject.ArticifialIntelligence;
using Unity.Burst;
using Unity.Entities;

namespace GameProject.ChatApp
{
    internal partial struct ChatUserProcessInactiveCharactersSystem : ISystem, ISystemStartStop
    {
        private TimerComponent _lastUpdateElapsedTime;
        private ChatBotInactivitySettings _chatGameAppSettings;
        private EntityQuery _chatBotInactivitySettingsQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ChatBotInactivitySettings>();
            state.RequireForUpdate<ActiveUserData>();
        }

        public void OnStartRunning(ref SystemState state)
        {
            _chatGameAppSettings = SystemAPI.GetSingleton<ChatBotInactivitySettings>();
            _lastUpdateElapsedTime.SetTimer(SystemAPI.Time.ElapsedTime, _chatGameAppSettings.SystemUpdateRateInSeconds);
            _chatBotInactivitySettingsQuery = SystemAPI.QueryBuilder().WithAll<ChatBotInactivitySettings>().Build();
            _chatBotInactivitySettingsQuery.SetChangedVersionFilter<ChatBotInactivitySettings>();
        }

        [BurstCompile]
        void ISystem.OnUpdate(ref SystemState state)
        {
#if UNITY_EDITOR
            _chatBotInactivitySettingsQuery.TryGetSingleton(out _chatGameAppSettings);
#endif
            double currentElapsedTime = SystemAPI.Time.ElapsedTime;
            if ( _lastUpdateElapsedTime.Tick(currentElapsedTime, _chatGameAppSettings.SystemUpdateRateInSeconds) )
            {
                foreach ( var (activeUserData, agentBrainStateComponentRW)
                         in SystemAPI.Query<ActiveUserData, RefRW<AgentBrainStateComponent>>() )
                {
                    ref var agentBrainStateComponent = ref agentBrainStateComponentRW.ValueRW;
                    if ( currentElapsedTime >= activeUserData.ActiveUntil
                         && agentBrainStateComponent.AgentBrainState is not AgentBrainState.GoToPartingLocation
                         && agentBrainStateComponent.AgentBrainState is not AgentBrainState.CharacterReadyToLeaveGame )
                    {
                        agentBrainStateComponent.AgentBrainState = AgentBrainState.GoToPartingLocation;
                    }
                }
            }
        }

        public void OnStopRunning(ref SystemState state)
        { }
    }
}