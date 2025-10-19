#if UNITY_EDITOR
using UnityEngine;
using System.Threading.Tasks;

namespace ChatBot.ChatProviders.Twitch
{
    [CreateAssetMenu(menuName = nameof(ChatBot) + "/Create TwitchChatUserMockAuthoring", fileName = "TwitchChatUserMockAuthoring")]
    public class TwitchChatUserMockAuthoring : ScriptableObject
    {
        [SerializeField] private TwitchChannelSettingsPersistentData _channelSettingsPersistentData = new() {
            Channel = "channelToConnectTo(usually the username of the host)",
            BotUserName = "botName",
        };
        private TwitchChannelSettingsPersistentData _channelSettingsPersistentDataPrevious;

        public string JoinCommandInput => _channelSettingsPersistentData.JoinCommandInput;

        private void OnValidate()
        {
            if ( _channelSettingsPersistentData.Equals(_channelSettingsPersistentDataPrevious) )
                return;
            
            _channelSettingsPersistentDataPrevious ??= new();
            _channelSettingsPersistentData.CopyTo(_channelSettingsPersistentDataPrevious);
            _ = SaveChatSettings();
        }

        private async Task SaveChatSettings()
        {
            await TwitchChatBotSettingsLoadingSystem.SaveChatSettings(_channelSettingsPersistentData, out var filePath).LogException();
            Debug.Log($"Saved {nameof(TwitchChannelSettingsPersistentData)} to {filePath}", this);
        }
    }
}
#endif