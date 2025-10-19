using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Properties;
using UnityEngine;

namespace ChatBot.Runtime
{
    [GeneratePropertyBag]
    public readonly struct ChatUser : IEquatable<ChatUser>
    {
        public readonly long UserId;
        public readonly ChatUserProvider ProviderSource;

        public ChatUser(ReadOnlySpan<char> userId, ChatUserProvider providerSource)
        {
            bool tryParse = long.TryParse(userId, NumberStyles.Integer, CultureInfo.InvariantCulture, out UserId);
            if ( !tryParse )
            {
                var parsedString = new FixedString128Bytes();
                for ( var i = 0; i < userId.Length; i++ )
                {
                    parsedString.Append(userId[i]);
                }
                Debug.LogError($"[PANIC] Unable to parse ChatUser ID '{parsedString}'.");
            }
            ProviderSource = providerSource;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ChatUser(long userId, ChatUserProvider providerSource)
        {
            UserId = userId;
            ProviderSource = providerSource;
        }

        public bool Equals(ChatUser other)
        {
            return UserId == other.UserId && ProviderSource.Id == other.ProviderSource.Id;
        }

        public override bool Equals(object obj)
        {
            return obj is ChatUser other && Equals(other);
        }

        public override int GetHashCode()
        {
            return UserId.GetHashCode();
        }

        public static ChatUser AsHostUser()
        {
            return new ChatUser(0, UserProviderIdExtensions.AsHostUser());
        }

        public static bool operator ==(ChatUser left, ChatUser right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ChatUser left, ChatUser right)
        {
            return !left.Equals(right);
        }
    }

    public struct ChatUserComponent : IComponentData, IEquatable<ChatUserComponent>
    {
        public ChatUser UserId;
        public FixedString64Bytes UserScreenName;

        public bool Equals(ChatUserComponent other)
        {
            return UserId.Equals(other.UserId);
        }

        public override bool Equals(object obj)
        {
            return obj is ChatUserComponent other && Equals(other);
        }

        public override int GetHashCode()
        {
            return UserId.GetHashCode();
        }

        public static bool operator ==(ChatUserComponent left, ChatUserComponent right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ChatUserComponent left, ChatUserComponent right)
        {
            return !left.Equals(right);
        }
    }

    [Serializable]
    [GeneratePropertyBag]
    public struct ChatUserProvider : IEquatable<ChatUserProvider>
    {
        public byte Id;

        public bool Equals(ChatUserProvider other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return obj is ChatUserProvider other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(ChatUserProvider left, ChatUserProvider right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ChatUserProvider left, ChatUserProvider right)
        {
            return !left.Equals(right);
        }
    }
}