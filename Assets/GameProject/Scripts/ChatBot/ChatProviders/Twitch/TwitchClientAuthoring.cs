using System;
using ChatBot.Runtime;
using Unity.Entities;
using Unity.Properties;
using UnityEngine;

namespace ChatBot.ChatProviders.Twitch
{
    public class TwitchClientAuthoring : MonoBehaviour
    {
        [SerializeField] private string WebAuthServiceBaseUrl = "http://localhost/";
        public bool RevokeTokenOnLeave = true;

        [SerializeField] private string[] TwitchPermissions = {
            //required to be able to interact with channel users
            "user:write:chat", "user:read:chat", "user:manage:whispers",
            "whispers:read", "whispers:edit",
            "chat:read", "chat:edit",
            //to get more information about the users in the channel
            "user:read:blocked_users",
            "channel:read:subscriptions",
            "moderation:read", "moderation:read:chatters",
        };

        private class Baker : Baker<TwitchClientAuthoring>
        {
            public override void Bake(TwitchClientAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponentObject(entity, new TwitchBotAuthDevData {
                    TwitchScopeString = string.Join(" ", authoring.TwitchPermissions),
                    AuthProviderUrl = $"{authoring.WebAuthServiceBaseUrl}{ConfigInfo.AuthProviderRoute}",
                    WaitForAuthUrl = $"{authoring.WebAuthServiceBaseUrl}{ConfigInfo.WaitForAuthRoute}",
                    RevokeTokenOnLeave = authoring.RevokeTokenOnLeave,
                });
            }
        }
    }

    [Flags]
    public enum ChatCommandMode : byte
    {
        AllowWhisperCommand = 1 << 0,
        AllowChatCommand = 1 << 1,
    }

    public struct TwitchChatSettingsRuntimeData : IComponentData
    {
        public ChatCommandMode ChatCommandMode;
        public HashValue JoinCommandInput;
        public int JoinCommandInputLength;
    }

    public class TwitchBotAuthDevData : IComponentData
    {
        public bool RevokeTokenOnLeave;
        public string TwitchScopeString;
        public string AuthProviderUrl;
        public string WaitForAuthUrl;
    }

    [GeneratePropertyBag, Serializable]
    public class TwitchChannelSettingsPersistentData : IComponentData, IEquatable<TwitchChannelSettingsPersistentData>
    {
        public string Channel = "";
        public string BotUserName = "";
        public string JoinCommandInput = "join";
        public ChatCommandMode ChatCommandMode = ChatCommandMode.AllowWhisperCommand | ChatCommandMode.AllowChatCommand;

        public void CopyTo(TwitchChannelSettingsPersistentData twitchChannelSettingsPersistentData)
        {
            twitchChannelSettingsPersistentData.BotUserName = BotUserName;
            twitchChannelSettingsPersistentData.Channel = Channel;
            twitchChannelSettingsPersistentData.JoinCommandInput = JoinCommandInput;
            twitchChannelSettingsPersistentData.ChatCommandMode = ChatCommandMode;
        }

        public void SanitizeChatSettings()
        {
            Channel = SanitizeString(Channel);
            BotUserName = SanitizeString(BotUserName);
            JoinCommandInput = SanitizeString(JoinCommandInput);
        }


        private static string SanitizeString(string channelSettingsJoinCommandInput)
        {
            return ChatCommandInputTextData.SanitizeAuthoringCommandString(channelSettingsJoinCommandInput).ToString();
        }

        #region EqualityImplementation
        public bool Equals(TwitchChannelSettingsPersistentData other)
        {
            if ( other is null )
            {
                return false;
            }
            if ( ReferenceEquals(this, other) )
            {
                return true;
            }
            return Channel == other.Channel && BotUserName == other.BotUserName && JoinCommandInput == other.JoinCommandInput && ChatCommandMode == other.ChatCommandMode;
        }

        public override bool Equals(object obj)
        {
            if ( obj is null )
            {
                return false;
            }
            if ( ReferenceEquals(this, obj) )
            {
                return true;
            }
            if ( obj.GetType() != GetType() )
            {
                return false;
            }
            return Equals((TwitchChannelSettingsPersistentData)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Channel, BotUserName, JoinCommandInput, (int)ChatCommandMode);
        }

        public static bool operator ==(TwitchChannelSettingsPersistentData left, TwitchChannelSettingsPersistentData right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(TwitchChannelSettingsPersistentData left, TwitchChannelSettingsPersistentData right)
        {
            return !Equals(left, right);
        }
        #endregion

    }
}