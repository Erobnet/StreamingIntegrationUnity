using System;
using ChatBot.Runtime;
using GameProject.Persistence.CommonData;
using Unity.Collections;

namespace GameProject.Persistence
{
    public unsafe struct ChatUserBinaryAdapter : ICustomBinarySerializer<ChatUser>
    {
        public unsafe void Serialize(ref SerializationContext context, ReadOnlySpan<ChatUser> values)
        {
            int runtimeSize = sizeof(ChatUser);
            int diskSize = sizeof(long) + sizeof(ChatUserProvider);
            fixed ( void* ptr = values )
            {
                for ( int i = 0; i < values.Length; i++ )
                {
                    context.WriteRawPtr((byte*)ptr + (i * runtimeSize), diskSize);
                }
            }
        }

        public void Deserialize(ref DeserializationContext context, ChatUser* writeCollection, int elementLength)
        {
            for ( int i = 0; i < elementLength; i++ )
            {
                var userId = context.ReadNext<long>();
                var chatUserProvider = context.ReadNext<ChatUserProvider>();
                writeCollection[i] = new(userId, chatUserProvider);
            }
        }
    }
}