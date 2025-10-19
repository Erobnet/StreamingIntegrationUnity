using Unity.Entities;

namespace ChatBot.Runtime
{
    [BakingType]
    public struct ChatBotUserRootBakingSettings : IComponentData
    {
        public Entity ChatUserCharacterPrefab;
    }
}