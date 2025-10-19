using GameProject.Characters;
using GameProject.Persistence.CommonData;
using GameProject.Player;
using Unity.Burst;
using Unity.Entities;
using Unity.Profiling;

namespace GameProject.ChatApp
{
    public partial struct ChatCurrencyUpdateSystem : ISystem, ISystemStartStop
    {
        private ChatCurrencySettings _chatCurrencySettings;
        private EntityQuery _currencySettingsQuery;
        private double _lastUpdateElapsedTime;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ChatCurrencySettings>();
            _currencySettingsQuery = SystemAPI.QueryBuilder().WithAll<ChatCurrencySettings>().Build();
            _currencySettingsQuery.SetChangedVersionFilter<ChatCurrencySettings>();
        }

        [BurstCompile]
        public void OnStartRunning(ref SystemState state)
        {
            _chatCurrencySettings = SystemAPI.GetSingleton<ChatCurrencySettings>();
            _lastUpdateElapsedTime = SystemAPI.Time.ElapsedTime;
        }

        [BurstCompile]
        void ISystem.OnUpdate(ref SystemState state)
        {
#if UNITY_EDITOR
            if ( !_currencySettingsQuery.IsEmpty )
            {
                _chatCurrencySettings = _currencySettingsQuery.GetSingleton<ChatCurrencySettings>();
            }
#endif
            double currentElapsedTime = SystemAPI.Time.ElapsedTime;
            var deltaTimeSinceLastSystemUpdate = currentElapsedTime - _lastUpdateElapsedTime;
            if ( deltaTimeSinceLastSystemUpdate > _chatCurrencySettings.UpdateIntervalInSeconds )
            {
                double currencyGain = _chatCurrencySettings.ViewerRateMoneyPerSeconds * deltaTimeSinceLastSystemUpdate;
                var truncatedGain = (uint)currencyGain;
                //carry over the loss due to the cast through time to the next update
                deltaTimeSinceLastSystemUpdate = (currencyGain - truncatedGain) / _chatCurrencySettings.ViewerRateMoneyPerSeconds;
                _lastUpdateElapsedTime = currentElapsedTime - deltaTimeSinceLastSystemUpdate;
                EntityQuery characterCurrencyUpdate = SystemAPI.QueryBuilder()
                    .WithAllRW<GameCurrency>()
                    .WithAll<CharacterHierarchyHubData>()
                    .WithAbsent<LocalPlayerTag>()
                    .Build();
                new UpdateOverTimeChatCurrencyJob {
                    ChatCurrencyGain = truncatedGain
                }.Schedule(characterCurrencyUpdate);
            }
        }

        [BurstCompile]
        public partial struct UpdateOverTimeChatCurrencyJob : IJobEntity
        {
            private static readonly ProfilerMarker _ExecuteMarker = new(nameof(UpdateOverTimeChatCurrencyJob) + "Marker");

            public uint ChatCurrencyGain;

            public void Execute(ref GameCurrency gameCurrency)
            {
                _ExecuteMarker.Begin();
                gameCurrency.Value += ChatCurrencyGain;
                _ExecuteMarker.End();
            }
        }


        public void OnStopRunning(ref SystemState state)
        { }
    }
}