using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Properties;
using ChatCommandArgumentTextType = Unity.Collections.FixedString32Bytes;
using ChatCommandInputTextType = Unity.Collections.FixedString64Bytes;

namespace ChatBot.Runtime
{
    public struct ChatCommandInput
    {
        [CreateProperty] public ChatCommandTypeComponent TypeId;
        [CreateProperty] public ChatCommandArgumentTextType ArgumentText;
        [CreateProperty] public FixedString64Bytes OriginMessageID;
    }


    public struct ChatCommandArgumentText
    {
        public ChatCommandArgumentTextType Value;

        /// <inheritdoc cref="ChatCommandInputTextData.SanitizeAuthoringCommandString{T}"/>
        public static ChatCommandArgumentText SanitizeAuthoringCommandString(ReadOnlySpan<char> commandArgumentName)
        {
            return ChatCommandInputTextData.SanitizeAuthoringCommandString<ChatCommandArgumentTextType>(commandArgumentName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ChatCommandArgumentTextType(in ChatCommandArgumentText value)
        {
            return value.Value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ChatCommandArgumentText(in ChatCommandArgumentTextType value)
        {
            return new ChatCommandArgumentText {
                Value = value
            };
        }
    }

    public struct ChatCommandInputTextData
    {
        public ChatCommandInputTextType ChatCommandInputText;

        /// <inheritdoc cref="SanitizeAuthoringCommandString{T}(System.ReadOnlySpan{char})"/>
        public static ChatCommandInputTextType SanitizeAuthoringCommandString(ReadOnlySpan<char> commandArgumentName)
        {
            return SanitizeAuthoringCommandString<ChatCommandInputTextType>(commandArgumentName);
        }

        /// <inheritdoc cref="SanitizeAuthoringCommandString{T}(System.ReadOnlySpan{char})"/>
        public static void SanitizeAuthoringCommandString<T>(ReadOnlySpan<char> commandArgumentName, out T chatCommandInputText)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            chatCommandInputText = SanitizeAuthoringCommandString<T>(commandArgumentName);
        }

        /// <summary>
        /// remove all spaces from the input string
        /// </summary>
        /// <remarks> only utf8 is supported</remarks>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T SanitizeAuthoringCommandString<T>(ReadOnlySpan<char> commandArgumentName)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            T transformedString = default;
            transformedString.Length = commandArgumentName.Length;
            var writeIndex = 0;
            for ( int i = 0; i < commandArgumentName.Length; i++ )
            {
                var c = commandArgumentName[i];
                if ( char.IsWhiteSpace(c) )
                    continue;

                transformedString[writeIndex++] = (byte)c;
            }
            transformedString.Length = writeIndex;
            return transformedString;
        }

        /// <inheritdoc cref="TrySanitizeRuntimeCommandString{T}"/>
        public static bool TrySanitizeRuntimeCommandString(ReadOnlySpan<char> commandArgumentName, out ChatCommandInputTextType commandArgument)
        {
            return TrySanitizeRuntimeCommandString<ChatCommandInputTextType>(commandArgumentName, out commandArgument);
        }

        /// <inheritdoc cref="TrySanitizeRuntimeCommandString{T}"/>
        public static bool TrySanitizeRuntimeCommandArgumentString(ReadOnlySpan<char> commandArgumentName, out ChatCommandArgumentTextType commandArgument)
        {
            return TrySanitizeRuntimeCommandString(commandArgumentName, out commandArgument);
        }

        /// <summary>
        /// ensure the argument chars are lower case
        /// </summary>
        /// <returns>Whatever or not the input char is within capacity bounds</returns>
        public static bool TrySanitizeRuntimeCommandString<T>(ReadOnlySpan<char> commandArgumentName, out T commandText)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            var trimmedChars = commandArgumentName.Trim();
            commandText = default;
            int transformedStringLength = trimmedChars.Length;
#if UNITY_EDITOR
            if ( trimmedChars.Length > commandText.Capacity )
            {
                return false;
            }
#endif
            commandText.Length = transformedStringLength;
            for ( int i = 0; i < transformedStringLength; i++ )
            {
                var c = trimmedChars[i];
                commandText[i] = (byte)char.ToLowerInvariant(c);
            }
            return true;
        }
    }

    public struct ChatCommandRuntimeDefinition : IBufferElementData
    {
        public ChatCommandInputTextType ChatCommandInputText;
        public HashValue CommandInput;
        public ChatCommandTypeComponent CommandType;
    }

    public struct ChatUserText : ICleanupComponentData
    {
        public NativeText Text;
    }

    public struct HasNewTextTag : IComponentData, IEnableableComponent
    { }

    public struct ChatCommandTypeComponent : IComponentData, IEquatable<ChatCommandTypeComponent>
    {
        public int Value;

        public static implicit operator ChatCommandTypeComponent(int value)
        {
            return new ChatCommandTypeComponent {
                Value = value
            };
        }

        public bool Equals(ChatCommandTypeComponent other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is ChatCommandTypeComponent other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public static bool operator ==(ChatCommandTypeComponent left, ChatCommandTypeComponent right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ChatCommandTypeComponent left, ChatCommandTypeComponent right)
        {
            return !left.Equals(right);
        }
    }

    public struct ChatChannelOnline : IComponentData
    { }

    public struct UserCommandMetadata : IComponentData
    {
        public int MaxCommandsCharactersLength;
    }
}