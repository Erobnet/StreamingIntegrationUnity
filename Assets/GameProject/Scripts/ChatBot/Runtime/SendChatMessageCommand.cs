using Unity.Collections;
using Unity.Properties;
using ChatMessageTextString = Unity.Collections.FixedString512Bytes;

namespace ChatBot.Runtime
{
    public struct SendChatMessageCommand
    {
        [CreateProperty] public readonly FixedString64Bytes? ReplyMessageID;
        [CreateProperty] public ChatMessageTextString MessageText;

        public SendChatMessageCommand(ChatMessageTextString messageText, in FixedString64Bytes? replyMessageID = null)
        {
            ReplyMessageID = replyMessageID;
            MessageText = messageText;
        }
    }
}