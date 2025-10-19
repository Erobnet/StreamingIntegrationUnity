using Unity.Entities;

namespace ChatBot.Runtime
{
    public struct ChatBotGameAppSettings : IComponentData
    {
        public Entity ChatUserRootPrefab;
        public int MaxSupportedViewerCharacterCount;
    }
}