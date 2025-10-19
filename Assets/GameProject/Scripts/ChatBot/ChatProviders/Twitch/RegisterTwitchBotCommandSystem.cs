#if UNITY_EDITOR || DEBUG || OFFSTREAMCHAT
      #define APP_DEBUG_ENV
#endif
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ChatBot.Runtime;
using JetBrains.Annotations;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace ChatBot.ChatProviders.Twitch
{
    //twitch rate limit, make sure the bot is moderator so it enjoys higher message rate in the chat to handle replies
    //https://dev.twitch.tv/docs/chat/#:~:text=If%20the%20user%20is%20not,20%20messages%20per%2030%20seconds.&text=If%20the%20user%20is%20the,100%20messages%20per%2030%20seconds.
    [UpdateInGroup(typeof(ChatMessageProvidersSystemGroup))]
    [BurstCompile]
    public partial class RegisterTwitchBotCommandSystem : TwitchBotClientAuthSystem
    {
        private static readonly string _LogCategoryTitle = $"TwitchBotClient";
        private const double _BOT_REPLY_SPAM_COOLDOWN_INSECONDS_PER_USER = 1f;

        private EntityQuery _chatBotGameAppSettingsQuery;
        private TwitchChatSystemRuntimeData _twitchChatSystemRuntimeData;
        private ChatSystemRuntimeData _chatSystemRuntimeData;
        private UserCommandMetadata _userCommandMetadata;
        private bool _isStreamOnline;
        private bool? _isBotConnected;
        private EntityQuery _commandsFilterQuery;
        private string _joinedChannel;
        private Entity _chatSystemsRuntimeEntity;
        private PlayerUserScreenNameData? _setPlayerDisplayName;
        private TwitchChatSettingsRuntimeData _twitchChatCacheRuntimeData;

        protected override void OnCreate()
        {
            base.OnCreate();

            _commandsFilterQuery = SystemAPI.QueryBuilder()
                .WithAll<ChatCommandRuntimeDefinition>()
                .Build();
            _commandsFilterQuery.SetChangedVersionFilter(ComponentType.ReadWrite<ChatCommandRuntimeDefinition>());
            _twitchChatSystemRuntimeData = new() {
                CommandIdentifier = '!'
            };
            RequireForUpdate<UserCommandMetadata>();
            RequireForUpdate<ChatSystemRuntimeData>();
            RequireForUpdate<TwitchChatSettingsRuntimeData>();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            _userCommandMetadata = SystemAPI.GetSingleton<UserCommandMetadata>();
            _chatSystemRuntimeData = SystemAPI.GetSingleton<ChatSystemRuntimeData>();
            _chatSystemRuntimeData.SendChatBotMessageCommand.Add(UserProviderIdExtensions.AsTwitchUser(), new(100, Allocator.Persistent));
        }

        protected override void SubscribeTwitchBotEvents(TwitchChannelSettingsPersistentData twitchChannelCachePersistentData)
        {
            _twitchChatCacheRuntimeData = SystemAPI.GetSingleton<TwitchChatSettingsRuntimeData>();
#if APP_DEBUG_ENV
            //allow to use chat features offstream when in dev environment.
            //in a real scenario when the stream is offline, the streamer doesn't want viewers to start connecting/etc when they are not being broadcasted.
            ListenForChatUserInput(_twitchChatCacheRuntimeData);
#endif

            TwitchBotClient.OnConnected += async (sender, e) =>
            {
                LogHelper.LogInfoMessage($"The bot {e.BotUsername} successfully connected to Twitch.", _LogCategoryTitle);
                var broadCasterIDResponseTask = TwitchAPI.Helix.Users.GetUsersAsync(logins: new() {
                    twitchChannelCachePersistentData.Channel,
                });
                var broadCasterIDResponse = await broadCasterIDResponseTask.LogException();
                User broadCasterUser = broadCasterIDResponse.Users[0];
                _twitchChatSystemRuntimeData.BroadCasterUser = new(broadCasterUser.Id, UserProviderIdExtensions.AsTwitchUser());
                _setPlayerDisplayName = new() {
                    UserScreenName = broadCasterUser.DisplayName
                };

                _isBotConnected = true;
            };
            TwitchBotClient.OnDisconnected += (sender, e) =>
            {
                _isBotConnected = false;
                return Task.CompletedTask;
            };
            TwitchBotClient.OnJoinedChannel += (sender, e) =>
            {
                LogHelper.LogInfoMessage($"The bot {e.BotUsername} just joined the channel: {e.Channel}", _LogCategoryTitle);
#if APP_DEBUG_ENV
                _ = TwitchBotClient.SendMessageAsync(e.Channel, "I just joined the channel! PogChamp");
#endif
                _joinedChannel = e.Channel;
                foreach ( var chatCommandIdentifier in TwitchBotClient.ChatCommandIdentifiers )
                {
                    _twitchChatSystemRuntimeData.CommandIdentifier = chatCommandIdentifier;
                    break;
                }
                return Task.CompletedTask;
            };
            TwitchBotClient.OnLeftChannel += (sender, e) =>
            {
                _joinedChannel = null;
                if ( _chatSystemRuntimeData.SendChatBotMessageCommand.TryGetValue(UserProviderIdExtensions.AsTwitchUser(), out var chatMessageCommands) )
                {
                    chatMessageCommands.Clear();
                }

                return Task.CompletedTask;
            };
            ChannelMonitorService.OnStreamOnline += (sender, e) =>
            {
#if !APP_DEBUG_ENV
                ListenForChatUserInput(_twitchChatCacheRuntimeData);
#endif
                if ( e.Channel == twitchChannelCachePersistentData.Channel )
                {
                    _isStreamOnline = true;
                }
            };
            ChannelMonitorService.OnStreamOffline += (sender, e) =>
            {
#if !APP_DEBUG_ENV
                UnsubscribeListeners(_twitchChatCacheRuntimeData);
#endif
                if ( e.Channel == twitchChannelCachePersistentData.Channel )
                {
                    _isStreamOnline = false;
                }
            };
        }

        private void UnsubscribeListeners(TwitchChatSettingsRuntimeData twitchChatCacheRuntimeData)
        {
            TwitchBotClient.OnMessageReceived -= OnMessageReceivedCallback;

            if ( (twitchChatCacheRuntimeData.ChatCommandMode & ChatCommandMode.AllowChatCommand) != 0 )
            {
                TwitchBotClient.OnChatCommandReceived -= OnChatCommandReceived;
            }

            if ( (twitchChatCacheRuntimeData.ChatCommandMode & ChatCommandMode.AllowWhisperCommand) != 0 )
            {
                TwitchBotClient.OnWhisperCommandReceived -= OnWhisperCommandReceived;
            }
        }

        /// <summary>
        /// allow to receive chat users commands and messages
        /// </summary>
        /// <param name="twitchChatCacheRuntimeData"></param>
        private void ListenForChatUserInput(TwitchChatSettingsRuntimeData twitchChatCacheRuntimeData)
        {
            TwitchBotClient.OnMessageReceived += OnMessageReceivedCallback;

            if ( (twitchChatCacheRuntimeData.ChatCommandMode & ChatCommandMode.AllowChatCommand) != 0 )
            {
                TwitchBotClient.OnChatCommandReceived += OnChatCommandReceived;
            }

            if ( (twitchChatCacheRuntimeData.ChatCommandMode & ChatCommandMode.AllowWhisperCommand) != 0 )
            {
                TwitchBotClient.OnWhisperCommandReceived += OnWhisperCommandReceived;
            }
        }

        private Task OnWhisperCommandReceived(object sender, OnWhisperCommandReceivedArgs e)
        {
            ProcessCommands(e.Command, e.WhisperMessage); //can't reply to a whisper on the chat
            return Task.CompletedTask;
        }

        private Task OnChatCommandReceived(object sender, OnChatCommandReceivedArgs e)
        {
            ProcessCommands(e.Command, e.ChatMessage, e.ChatMessage.Id);
            return Task.CompletedTask;
        }

        private Task OnMessageReceivedCallback([CanBeNull] object sender, OnMessageReceivedArgs e)
        {
            if ( TwitchBotClient.ChatCommandIdentifiers.Contains(e.ChatMessage.Message[0]) )
                return Task.CompletedTask;

#if UNITY_EDITOR
            Debug.Log($"Message received from {e.ChatMessage.Username}: {e.ChatMessage.Message}");
#endif
            ChatUser chatUser = GetChatUser(e.ChatMessage);
            if ( _chatSystemRuntimeData.HasGameCharacter(in chatUser, out Entity chatUserRoot) )
            {
                FixedString512Bytes convertedMessage = default;
                convertedMessage.CopyFromTruncated(e.ChatMessage.Message);
                _chatSystemRuntimeData.ChatUserCharacterDisplayTextBuffer.Add((chatUserRoot, convertedMessage));
            }
            return Task.CompletedTask;
        }

        private void ProcessCommands(CommandInfo commandInfo, TwitchLibMessage message, string messageId = "")
        {
            if ( commandInfo.Name.Length > _userCommandMetadata.MaxCommandsCharactersLength )
                return; //early out commands that will not match or issues related from extra long unallowed commands

            var trimmedCommandName = commandInfo.Name.AsSpan().Trim();
            ChatUser chatUser = GetChatUser(message);
            bool hasCharacter = _chatSystemRuntimeData.HasGameCharacter(in chatUser, out var chatUserEntity);
            if ( hasCharacter )
            {
                Span<char> parsedCommandName = stackalloc char[commandInfo.Name.Length];
                trimmedCommandName.ToLowerInvariant(parsedCommandName);
                HashValue commandHash = HashValue.Create(parsedCommandName);
                if ( _chatSystemRuntimeData.AllowedCommandLookup.TryGetValue(commandHash, out ChatCommandTypeComponent commandType) )
                {
                    ChatCommandInputTextData.TrySanitizeRuntimeCommandArgumentString(commandInfo.ArgumentsAsString.AsSpan(), out var argumentText);
                    _chatSystemRuntimeData.PlayerCommandApplyQueue[chatUserEntity] = new() {
                        TypeId = commandType,
                        OriginMessageID = messageId,
                        ArgumentText = argumentText
                    };
                }
                else
                {
                    var chatCommandInputText = parsedCommandName.ToString();
                    var sendChatMessageCommandDefault = new SendChatMessageCommand($"'{chatCommandInputText}' is invalid. try: ", messageId);
                    var count = _chatSystemRuntimeData.CommandDescriptionLookup.Count;
                    foreach ( var commandDescription in _chatSystemRuntimeData.CommandDescriptionLookup )
                    {
                        sendChatMessageCommandDefault.MessageText.Append(_twitchChatSystemRuntimeData.CommandIdentifier);
                        sendChatMessageCommandDefault.MessageText.Append(commandDescription.Value.ChatCommandInputText);
                        if ( count > 1 )
                            sendChatMessageCommandDefault.MessageText.Append(" ,");

                        count--;
                    }
                    _chatSystemRuntimeData.SendReplyTo(in chatUser, sendChatMessageCommandDefault, World.Time.ElapsedTime);
                }
            }
            //this is the only command allowed for non character and therefore could be spammed by the whole chat without limits so we are trying to make it as light as possible
            else if ( trimmedCommandName.Length == _twitchChatCacheRuntimeData.JoinCommandInputLength
                      && _twitchChatCacheRuntimeData.JoinCommandInput == HashValue.Create(trimmedCommandName) )
            {
                _chatSystemRuntimeData.AddPlayerToJoinQueue(new PlayerJoinQueueInfo {
                    ChatUserInfo = new ChatUserComponent {
                        UserId = chatUser,
                        UserScreenName = message.DisplayName
                    },
                });
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ChatUser GetChatUser(TwitchLibMessage message)
        {
            return new(message.UserId, UserProviderIdExtensions.AsTwitchUser());
        }

        protected override void OnUpdate()
        {
            if ( _setPlayerDisplayName.HasValue && !EntityManager.HasComponent<PlayerUserScreenNameData>(SystemHandle) )
            {
                EntityManager.AddComponentData(SystemHandle, _setPlayerDisplayName.Value);
                _setPlayerDisplayName = null;
            }

            if ( !_commandsFilterQuery.IsEmpty )
            {
                InitializeCommandDescriptions(_commandsFilterQuery.GetSingletonBuffer<ChatCommandRuntimeDefinition>().ToNativeArray(Allocator.Temp));
            }

            if ( _isBotConnected.HasValue )
            {
                if ( _isBotConnected.Value ) //ensure we have the bot and broadcaster id 
                {
                    if ( !_chatSystemRuntimeData.HostIdentities.Contains(_twitchChatSystemRuntimeData.BroadCasterUser) )
                    {
                        _chatSystemRuntimeData.HostIdentities.Add(in _twitchChatSystemRuntimeData.BroadCasterUser);
                    }
                    EntityManager.AddComponentData(SystemHandle, _twitchChatSystemRuntimeData);
                }
                else if ( EntityManager.HasComponent<ChatSystemRuntimeData>(SystemHandle) )
                {
                    int foundIndex = _chatSystemRuntimeData.HostIdentities.FindFirstIndexOf(_twitchChatSystemRuntimeData.BroadCasterUser);
                    if ( foundIndex != -1 )
                    {
                        _chatSystemRuntimeData.HostIdentities.RemoveAtSwapBack(foundIndex);
                    }
                    EntityManager.RemoveComponent<TwitchChatSystemRuntimeData>(SystemHandle);
                }
                _isBotConnected = null;
            }

            if ( _isStreamOnline )
            {
                if ( !EntityManager.HasComponent<ChatChannelOnline>(SystemHandle) )
                {
                    EntityManager.AddComponent<ChatChannelOnline>(SystemHandle);
                }
#if !APP_DEBUG_ENV
                HandleBotReplyMessages();
#endif
            }
            else if ( EntityManager.HasComponent<ChatChannelOnline>(SystemHandle) )
            {
                EntityManager.RemoveComponent<ChatChannelOnline>(SystemHandle);
            }

#if APP_DEBUG_ENV
            HandleBotReplyMessages();
#endif
        }

        private void HandleBotReplyMessages()
        {
            if ( _joinedChannel == null )
                return;

            var elapsedTime = World.Time.ElapsedTime;
            var receiverKeys = _chatSystemRuntimeData.SendBotReplyCooldownPerUserLookup.AsKeysArray();
            var botReplyCooldowns = _chatSystemRuntimeData.SendBotReplyCooldownPerUserLookup.AsValuesArray();
            for ( int i = _chatSystemRuntimeData.SendBotReplyCooldownPerUserLookup.Length - 1; i >= 0; i-- )
            {
                var receiverKey = receiverKeys[i];
                var botLastReplyTime = botReplyCooldowns[i];
                if ( elapsedTime > (botLastReplyTime + _BOT_REPLY_SPAM_COOLDOWN_INSECONDS_PER_USER) )
                {
                    _chatSystemRuntimeData.SendBotReplyCooldownPerUserLookup.TryRemove(receiverKey);
                }
            }

            var sendChatMessageCommands = _chatSystemRuntimeData.SendChatBotMessageCommand[UserProviderIdExtensions.AsTwitchUser()];
            for ( var index = sendChatMessageCommands.Length - 1; index >= 0; index-- )
            {
                ref readonly var sendChatMessageCommand = ref sendChatMessageCommands.ElementAt(index);
                if ( sendChatMessageCommand.ReplyMessageID.HasValue )
                {
                    _ = TwitchBotClient.SendReplyAsync(_joinedChannel, sendChatMessageCommand.ReplyMessageID.ToString(), sendChatMessageCommand.MessageText.ToString());
                }
                else
                {
                    _ = TwitchBotClient.SendMessageAsync(_joinedChannel, sendChatMessageCommand.MessageText.ToString());
                }
            }
            sendChatMessageCommands.Clear();
        }

        private void InitializeCommandDescriptions(NativeArray<ChatCommandRuntimeDefinition> chatCommandDescriptions)
        {
            _chatSystemRuntimeData.AllowedCommandLookup.Clear();
            _chatSystemRuntimeData.CommandDescriptionLookup.Clear();
            for ( var index = 0; index < chatCommandDescriptions.Length; index++ )
            {
                var commandDescription = chatCommandDescriptions[index];
                _chatSystemRuntimeData.AllowedCommandLookup.Add(commandDescription.CommandInput, commandDescription.CommandType);
                _chatSystemRuntimeData.CommandDescriptionLookup.Add(commandDescription.CommandType, new() {
                    ChatCommandInputText = commandDescription.ChatCommandInputText,
                });
            }
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            _chatSystemRuntimeData.SendChatBotMessageCommand[UserProviderIdExtensions.AsTwitchUser()].Dispose();
            _chatSystemRuntimeData.SendChatBotMessageCommand.Remove(UserProviderIdExtensions.AsTwitchUser());
        }
    }

    public static partial class UserProviderIdExtensions
    {
        private static readonly ChatUserProvider _TwitchChatUserProvider = new() {
            Id = 2
        };

        public static ChatUserProvider AsTwitchUser()
        {
            return _TwitchChatUserProvider;
        }

        public static ChatUserProvider AsTwitchUser(this ChatUserProvider chatUserProvider)
        {
            return AsTwitchUser();
        }

        public static bool IsTwitchUser(this in ChatUser chatUser)
        {
            return chatUser.ProviderSource.Id == _TwitchChatUserProvider.Id;
        }
    }
}