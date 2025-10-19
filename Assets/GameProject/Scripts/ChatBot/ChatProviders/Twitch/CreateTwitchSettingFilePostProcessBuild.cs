#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ChatBot.ChatProviders.Twitch
{
    public class CreateTwitchSettingFilePostProcessBuild : IPostprocessBuildWithReport
    {
        public int callbackOrder {
            get;
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            string buildOutputPath = Path.GetDirectoryName(report.summary.outputPath);
            System.Diagnostics.Debug.Assert(buildOutputPath != null, nameof(buildOutputPath) + " was path was null");
            string settingsDirectory = Path.Combine(buildOutputPath, ProjectFilePaths.USERSETTINGS_DIRECTORY_NAME);
            var projectTwitchSettings = TwitchChatBotSettingsLoadingSystem.GetTwitchChannelSettingsPersistentData();
            var twitchChannelSettingsPersistentData = new TwitchChannelSettingsPersistentData() {
                JoinCommandInput = projectTwitchSettings.JoinCommandInput,
            };

            string twitchChatBotSettingsFilePath = TwitchChatBotSettingsLoadingSystem.GetChatBotSettingsFilePath(settingsDirectory);
            TwitchChatBotSettingsLoadingSystem.EnsureCreateSettingsFolderAndFile(settingsDirectory, twitchChatBotSettingsFilePath, twitchChannelSettingsPersistentData);
            Debug.Log($"Created {nameof(TwitchChatBotSettingsLoadingSystem)} settings file at {twitchChatBotSettingsFilePath}");
        }
    }
}
#endif