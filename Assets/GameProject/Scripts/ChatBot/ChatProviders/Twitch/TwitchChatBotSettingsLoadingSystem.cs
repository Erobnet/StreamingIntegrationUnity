using System.IO;
using System.Threading.Tasks;
using ChatBot.Runtime;
using Unity.Entities;
using Unity.Serialization.Json;
using static ProjectFilePaths;

namespace ChatBot.ChatProviders.Twitch
{

    public partial class TwitchChatBotSettingsLoadingSystem : SystemBase
    {
        private const string _TWITCH_CHAT_BOT_SETTINGS_FILE_NAME = "TwitchChatBotSettings.json";
        private static string _baseChatBotSettingsFilePath;


        public static string SettingsFilePath {
            get {
                InitializeSettingsPaths();
                return _baseChatBotSettingsFilePath;
            }
        }

        private static void InitializeSettingsPaths()
        {
            if ( _baseChatBotSettingsFilePath is not null )
                return;
            _baseChatBotSettingsFilePath = GetChatBotSettingsFilePath(SettingsDirectory);
        }

        public static string GetChatBotSettingsFilePath(string settingsDirectory)
        {
            return Path.Combine(settingsDirectory, _TWITCH_CHAT_BOT_SETTINGS_FILE_NAME);
        }

        public static void EnsureCreateSettingsFolderAndFile(string settingsDirectory, string settingsFilePath, TwitchChannelSettingsPersistentData twitchChannelSettingsPersistentData)
        {
            Directory.CreateDirectory(settingsDirectory);
            if ( !File.Exists(settingsFilePath) )
            {
                File.WriteAllText(settingsFilePath, JsonSerialization.ToJson(twitchChannelSettingsPersistentData));
            }
        }

        protected override void OnCreate()
        {
            EnsureCreateSettingsFolderAndFile(SettingsDirectory, SettingsFilePath, new());
        }

        protected override void OnStartRunning()
        {
            if ( !File.Exists(SettingsFilePath) )
                return;

            var chatSettingsPersistentData = GetTwitchChannelSettingsPersistentData();
            EntityManager.AddComponentData(SystemHandle, chatSettingsPersistentData);
            EntityManager.AddComponentData(SystemHandle, new TwitchChatSettingsRuntimeData {
                ChatCommandMode = chatSettingsPersistentData.ChatCommandMode,
                JoinCommandInput = HashValue.Create(chatSettingsPersistentData.JoinCommandInput),
                JoinCommandInputLength = chatSettingsPersistentData.JoinCommandInput.Length
            });
        }

        public static TwitchChannelSettingsPersistentData GetTwitchChannelSettingsPersistentData()
        {
            using var fileStream = File.OpenRead(SettingsFilePath);
            var chatSettingsData = JsonSerialization.FromJson<TwitchChannelSettingsPersistentData>(fileStream);
            chatSettingsData.SanitizeChatSettings();
            return chatSettingsData;
        }

        public static Task SaveChatSettings(TwitchChannelSettingsPersistentData twitchChannelSettingsPersistentData, out string filePath)
        {
            var chatSettingsAsString = JsonSerialization.ToJson(twitchChannelSettingsPersistentData);
            filePath = SettingsFilePath;
            return File.WriteAllTextAsync(_baseChatBotSettingsFilePath, chatSettingsAsString);
        }

        protected override void OnUpdate()
        { }
    }
}