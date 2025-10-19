using System;
using ChatBot.Runtime;
using GameProject.Characters;
using GameProject.ItemManagement;
using GameProject.UI;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace GameProject.ChatApp
{
    public enum UserChatCommandType
    {
        None = 0,
        RollCharacterSkinOption,
        WaveSocial,
        OrderItem,
        LeaveGame,
    }

    [Serializable]
    public class AvailableItemsFromArgumentsDescription
    {
        [FormerlySerializedAs("ItemAsset")] public ItemAssetAuthoring itemAssetAuthoring;
        [Tooltip("Optional: if empty, a parsed (lower case without spaces) name of the " + nameof(itemAssetAuthoring) + " property will be used")]
        public string CommandArgumentName;
    }

    [Serializable]
    public class ChatCommandDescriptionAuthoring
    {
        public string ChatCommandText;
        public UserChatCommandType CommandType;
        public AvailableItemsFromArgumentsDescription[] AvailableItemsFromArguments = Array.Empty<AvailableItemsFromArgumentsDescription>();
    }

    public class ChatGameAppAuthoring : MonoBehaviour
    {

        [SerializeField] private float inactivityDelaySeconds = 120;
        [SerializeField] private GameCharacterAuthoring chatUserGameCharacterPrefab;
        [SerializeField] private ChatUserRootAuthoring chatUserRootPrefab;
        [SerializeField] private ChatCommandDescriptionAuthoring[] commandStrings = Array.Empty<ChatCommandDescriptionAuthoring>();
        [SerializeField] private uint maxSupportedViewerCharacterCount = 100;
        [SerializeField] private float chatCurrencyUpdateIntervalSeconds = 1;
        [SerializeField] private float chatUserCurrencyGainPerMinute = 60;
        [SerializeField] private WorldCharacterUI worldCharacterUIPrefab;
        [SerializeField] private Vector3 ChatBubblePositionOffset = new(0, .8f, 0);
        [SerializeField, HideInInspector] private WorldCharacterUIPrefabComponentRef worldCharacterUIPrefabComponentRef;

#if UNITY_EDITOR
        private static readonly string _WorldUIRefFileName = nameof(worldCharacterUIPrefabComponentRef);
        private static readonly string _WorldUIRefFilePath = PrefabComponentRefExtensions.ConstructPrefabRefAssetPath<ChatGameAppAuthoring>(_WorldUIRefFileName);

        private void OnValidate()
        {
            ManageWorldUIPrefabComponentRef();
            if ( ValidateChatCommandDescriptions() )
            {
                this.SetDirtySafe();
            }
        }

        private bool ValidateChatCommandDescriptions()
        {
            var isDirty = false;
            for ( var commandIndex = 0; commandIndex < commandStrings.Length; commandIndex++ )
            {
                var chatCommandDescriptionAuthoring = commandStrings[commandIndex];
                var chatCommandText = SanitizeChatCommandForBaking(chatCommandDescriptionAuthoring.ChatCommandText);
                isDirty |= !object.Equals(chatCommandText, chatCommandDescriptionAuthoring.ChatCommandText);
                chatCommandDescriptionAuthoring.ChatCommandText = chatCommandText;
                for ( var i = 0; i < chatCommandDescriptionAuthoring.AvailableItemsFromArguments.Length; i++ )
                {
                    var itemsFromArgumentsDescription = chatCommandDescriptionAuthoring.AvailableItemsFromArguments[i];
                    var validCommandArgumentName = SanitizeChatCommandForBaking(itemsFromArgumentsDescription.CommandArgumentName);
                    isDirty |= !object.Equals(validCommandArgumentName, itemsFromArgumentsDescription.CommandArgumentName);
                    itemsFromArgumentsDescription.CommandArgumentName = validCommandArgumentName;
                }
            }
            return isDirty;
        }

        private static string SanitizeChatCommandForBaking(ReadOnlySpan<char> commandArgumentName)
        {
            if ( commandArgumentName.IsEmpty )
                return "";

            Span<char> transformedString = stackalloc char[commandArgumentName.Length];
            var writeIndex = 0;
            for ( int i = 0; i < commandArgumentName.Length; i++ )
            {
                var c = commandArgumentName[i];
                if ( char.IsWhiteSpace(c) )
                    continue;

                (transformedString)[writeIndex++] = char.ToLowerInvariant(c);
            }
            return transformedString.Slice(0, writeIndex).ToString();
        }

        private void ManageWorldUIPrefabComponentRef()
        {
            PrefabComponentRefExtensions.ManagePrefabComponentRef(ref worldCharacterUIPrefabComponentRef, worldCharacterUIPrefab, _WorldUIRefFilePath);
        }
#endif

        private class Baker : Baker<ChatGameAppAuthoring>
        {
            private const int _EXTRA_PADDING_FOR_USER_MISTYPING = 2;
            private const int _MAX_ALLOWED_CHAR_LENGTH_VALUE = 62 - _EXTRA_PADDING_FOR_USER_MISTYPING;
            private const byte _DEFAULT_COMMAND_LENGTH = 4;

            public override void Bake(ChatGameAppAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                byte maxCommandsCharactersLength = _DEFAULT_COMMAND_LENGTH;
                var commandDescriptions = AddBuffer<ChatCommandRuntimeDefinition>(entity);
                foreach ( var commandDescription in authoring.commandStrings )
                {
                    var commandString = commandDescription.ChatCommandText;
                    if ( string.IsNullOrWhiteSpace(commandString) )
                    {
                        Debug.LogError($"a command has no {nameof(commandDescription.ChatCommandText)} and will be discarded.", authoring);
                        continue;
                    }

                    if ( commandDescription.CommandType == UserChatCommandType.None )
                    {
                        Debug.LogError($"Command {commandDescription.ChatCommandText} has no command type and will be discarded.", authoring);
                        continue;
                    }

                    if ( commandString.Length > _MAX_ALLOWED_CHAR_LENGTH_VALUE )
                    {
                        Debug.LogWarning($"Command character length is of ({commandString.Length}) length which is greater than the limit ({_MAX_ALLOWED_CHAR_LENGTH_VALUE}) characters. this command '{commandString}' will be truncated to fit that length", authoring);
                    }

                    byte validatedLength = (byte)math.min(commandString.Length, _MAX_ALLOWED_CHAR_LENGTH_VALUE);

                    if ( validatedLength > maxCommandsCharactersLength )
                    {
                        maxCommandsCharactersLength = validatedLength;
                    }

                    string parsedCommand = commandString;
                    var runtimeCommand = new ChatCommandRuntimeDefinition {
                        CommandInput = HashValue.Create(parsedCommand.AsSpan(0, validatedLength)),
                        ChatCommandInputText = parsedCommand,
                        CommandType = (int)commandDescription.CommandType,
                    };
                    commandDescriptions.Add(runtimeCommand);

                    if ( commandDescription.AvailableItemsFromArguments.Length == 0 )
                        continue;

                    var commandDataEntity = CreateAdditionalEntity(TransformUsageFlags.None, entityName: $"ChatCommandEntity({commandDescription.CommandType})");
                    AddComponent(commandDataEntity, runtimeCommand.CommandType);
                    var commandRequestItems = AddBuffer<ChatCommandAvailableItem>(commandDataEntity);
                    for ( uint index = 0; index < commandDescription.AvailableItemsFromArguments.Length; index++ )
                    {
                        var argumentDescription = commandDescription.AvailableItemsFromArguments[index];
                        if ( argumentDescription.itemAssetAuthoring == null )
                            continue;

                        var selectedArgumentString = string.IsNullOrWhiteSpace(argumentDescription.CommandArgumentName)
                            ? SanitizeChatCommandForBaking(argumentDescription.itemAssetAuthoring.name)
                            : argumentDescription.CommandArgumentName;// already validated by the authoring onvalidate
                        ChatCommandAvailableItem chatCommandAvailableItem = new() {
                            ItemAssetDataReference = this.GetOrCreateItemAssetDataRef(argumentDescription.itemAssetAuthoring),
                            CharacterTransitionData = argumentDescription.itemAssetAuthoring.CharacterTransitionData,
                            ColorOptions = argumentDescription.itemAssetAuthoring.ColorOptions,
                            ArgumentString = new() {
                                Value = selectedArgumentString
                            }
                        };
                        chatCommandAvailableItem.ArgumentHash = HashValue.Create(chatCommandAvailableItem.ArgumentString.Value);
                        commandRequestItems.Add(chatCommandAvailableItem);
                    }
                }

                AddComponent(entity, new UserCommandMetadata {
                    MaxCommandsCharactersLength = maxCommandsCharactersLength + _EXTRA_PADDING_FOR_USER_MISTYPING
                });
                var chatBotGameAppSettings = new ChatBotGameAppSettings {
                    MaxSupportedViewerCharacterCount = (int)authoring.maxSupportedViewerCharacterCount,
                    ChatUserRootPrefab = GetEntity(authoring.chatUserRootPrefab, TransformUsageFlags.None),
                };

                AddComponent(entity, new ChatBotInactivitySettings {
                    InactivityDelaySeconds = authoring.inactivityDelaySeconds,
                    SystemUpdateRateInSeconds = authoring.inactivityDelaySeconds / 10f
                });
                AddComponent(entity, chatBotGameAppSettings);
                AddComponent(entity, new ChatBotUserRootBakingSettings {
                    ChatUserCharacterPrefab = GetEntity(authoring.chatUserGameCharacterPrefab, TransformUsageFlags.None),
                });
                AddComponent(entity, new ChatCurrencySettings {
                    ViewerRateMoneyPerSeconds = authoring.chatUserCurrencyGainPerMinute / 60,
                    UpdateIntervalInSeconds = authoring.chatCurrencyUpdateIntervalSeconds,
                });
                AddComponent<HasBakedCharacterViewerPrefab>(entity);
                AddComponent<ChatBotAppTag>(entity);

                DependsOn(authoring.worldCharacterUIPrefabComponentRef);
                DependsOn(authoring.worldCharacterUIPrefab);
                AddComponent(entity, new ChatBubbleGameObjectPrefab {
                    Prefab = authoring.worldCharacterUIPrefabComponentRef,
                    ChatBubblePositionOffset = authoring.ChatBubblePositionOffset
                });
            }
        }
    }

    public struct ChatCommandAvailableItem : IBufferElementData
    {
        public ItemAssetDataReference ItemAssetDataReference;
        public UnityObjectRef<CharacterTransitionData> CharacterTransitionData;
        public UnityObjectRef<Material> ColorOptions;
        public HashValue ArgumentHash;
        public ChatCommandArgumentText ArgumentString;
    }

    public struct ChatBubbleGameObjectPrefab : IComponentData
    {
        public UnityObjectRef<WorldCharacterUIPrefabComponentRef> Prefab;
        public float3 ChatBubblePositionOffset;
    }

    public struct ChatBotInactivitySettings : IComponentData
    {
        public float InactivityDelaySeconds;
        public float SystemUpdateRateInSeconds;
    }

    public struct ActiveUserData : IComponentData
    {
        public float ActiveUntil;
    }
}