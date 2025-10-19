using Drboum.Utilities.Collections;
using Unity.Assertions;
using Unity.Collections;
using Unity.Entities;

namespace ChatBot.Runtime
{
    /// <summary>
    /// struct used to communicate data between the chat API layers and the game world
    /// </summary>
    public struct ChatSystemRuntimeData : IComponentData
    {
        public NativeFastReadHashMap<Entity, ChatCommandInput> PlayerCommandApplyQueue;
        public NativeHashMap<ChatUserProvider, NativeList<SendChatMessageCommand>> SendChatBotMessageCommand;
        public NativeFastReadHashMap<ChatUser, double> SendBotReplyCooldownPerUserLookup;
        public NativeHashMap<ChatUser, Entity> IngamePlayerLookup;
        public NativeHashMap<HashValue, ChatCommandTypeComponent> AllowedCommandLookup;
        public NativeHashMap<ChatCommandTypeComponent, ChatCommandInputTextData> CommandDescriptionLookup;
        public NativeList<(Entity ChatUserRoot, FixedString512Bytes text)> ChatUserCharacterDisplayTextBuffer;
        public NativeQueue<PlayerJoinQueueInfo> PlayerJoinQueue;
        public NativeList<ChatUser> PlayerLeaveQueue;
        public NativeList<ChatUser> HostIdentities;

        public ChatSystemRuntimeData(Allocator allocator)
        {
            SendBotReplyCooldownPerUserLookup = new(100, allocator);
            PlayerCommandApplyQueue = new(100, allocator);
            CommandDescriptionLookup = new(25, allocator);
            IngamePlayerLookup = new(50, allocator);
            AllowedCommandLookup = new(25, allocator);
            HostIdentities = new(5, allocator);
            PlayerJoinQueue = new(allocator);
            PlayerLeaveQueue = new(50, allocator);
            SendChatBotMessageCommand = new(50, allocator);
            ChatUserCharacterDisplayTextBuffer = new(50, allocator);
        }

        internal void ResetChatCollections()
        {
            SendChatBotMessageCommand.Clear();
            PlayerCommandApplyQueue.Clear();
            PlayerJoinQueue.Clear();
            HostIdentities.Clear();
            ChatUserCharacterDisplayTextBuffer.Clear();
        }

        public bool HasGameCharacter(in ChatUser user, out Entity root)
        {
            return IngamePlayerLookup.TryGetValue(user, out root) && root != Entity.Null;
        }

        public void AddPlayerToJoinQueue(PlayerJoinQueueInfo playerInfo)
        {
            if ( IngamePlayerLookup.TryAdd(playerInfo.ChatUserInfo.UserId, Entity.Null) )
            {
                PlayerJoinQueue.Enqueue(playerInfo);
            }
        }

        public void AddPlayerToLeaveQueue(in ChatUser chatUser)
        {
            PlayerLeaveQueue.Add(in chatUser);
        }

        public void SendReplyTo(in ChatUser chatUser, in SendChatMessageCommand sendChatMessageCommand, double elapsedTimeSeconds)
        {
            Assert.AreNotEqual(chatUser, default);
            Assert.AreNotEqual(chatUser.ProviderSource, default);
            if ( SendBotReplyCooldownPerUserLookup.TryAdd(chatUser, elapsedTimeSeconds)
                 && SendChatBotMessageCommand.TryGetValue(chatUser.ProviderSource, out var sendChatMessageCommands) )
            {
                sendChatMessageCommands.Add(sendChatMessageCommand);
            }
        }

        public bool BotCanSendReplyToUser(in ChatUser chatUser)
        {
            return SendBotReplyCooldownPerUserLookup.Contains(in chatUser);
        }

        public void Dispose()
        {
            SendBotReplyCooldownPerUserLookup.Dispose();
            PlayerCommandApplyQueue.Dispose();
            IngamePlayerLookup.Dispose();
            CommandDescriptionLookup.Dispose();
            AllowedCommandLookup.Dispose();
            PlayerJoinQueue.Dispose();
            foreach ( var chatBotMessageListPerProvider in SendChatBotMessageCommand )
            {
                chatBotMessageListPerProvider.Value.Dispose();
            }
            SendChatBotMessageCommand.Dispose();
            HostIdentities.Dispose();
            PlayerLeaveQueue.Dispose();
            ChatUserCharacterDisplayTextBuffer.Dispose();
        }
    }

    public struct PlayerJoinQueueInfo
    {
        public ChatUserComponent ChatUserInfo;
    }

    public static partial class UserProviderIdExtensions
    {
        public static readonly ChatUserProvider HostChatUserProvider = new() {
            Id = 1
        };

        public static ChatUserProvider AsHostUser()
        {
            return HostChatUserProvider;
        }

        public static ChatUserProvider AsHostUser(this ChatUserProvider chatUserProvider)
        {
            return AsHostUser();
        }

        public static bool IsHostUser(this in ChatUser chatUser)
        {
            return chatUser.ProviderSource.Id == HostChatUserProvider.Id;
        }
    }
}