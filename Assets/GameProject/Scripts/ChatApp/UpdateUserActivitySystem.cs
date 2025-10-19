using ChatBot.Runtime;
using Unity.Burst;
using Unity.Entities;
using Unity.Profiling;

namespace GameProject.ChatApp
{
    /// <summary>
    /// update the user leaving timeout whenever an user do something noteworthy
    /// </summary>
    [UpdateInGroup(typeof(ChatBotSystemGroup))]
    [UpdateAfter(typeof(ApplyChatUserTextFromChatSystem))] //update the text tag which this system rely on.
    public partial struct UpdateUserActivitySystem : ISystem, ISystemStartStop
    {
        private ChatBotInactivitySettings _chatBotChatBotInactivitySettings;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ChatBotInactivitySettings>();
        }

        [BurstCompile]
        public void OnStartRunning(ref SystemState state)
        {
            _chatBotChatBotInactivitySettings = SystemAPI.GetSingleton<ChatBotInactivitySettings>();
        }

        [BurstCompile]
        void ISystem.OnUpdate(ref SystemState state)
        {
#if UNITY_EDITOR
            _chatBotChatBotInactivitySettings = SystemAPI.GetSingleton<ChatBotInactivitySettings>();
#endif
            EntityQuery newTextArrived = SystemAPI.QueryBuilder()
                .WithAllRW<ActiveUserData>()
                .WithAll<HasNewTextTag>()
                .Build();
            new UpdateActivityTimeJob {
                UpdatedActivityTime = (float)(SystemAPI.Time.ElapsedTime + _chatBotChatBotInactivitySettings.InactivityDelaySeconds)
            }.Run(newTextArrived);
        }

        [BurstCompile]
        public partial struct UpdateActivityTimeJob : IJobEntity
        {
            private static readonly ProfilerMarker _ExecuteMarker = new(nameof(UpdateActivityTimeJob) + "Marker");

            public float UpdatedActivityTime;

            public void Execute(ref ActiveUserData activeUserData)
            {
                _ExecuteMarker.Begin();
                activeUserData.ActiveUntil = UpdatedActivityTime;
                _ExecuteMarker.End();
            }
        }

        public void OnStopRunning(ref SystemState state)
        { }
    }
}