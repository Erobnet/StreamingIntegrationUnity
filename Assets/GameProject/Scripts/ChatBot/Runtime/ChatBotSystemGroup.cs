using Unity.Collections;
using Unity.Entities;

namespace ChatBot.Runtime
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ChatBotSystemGroup : ComponentSystemGroup
    { }

    [UpdateInGroup(typeof(ChatBotSystemGroup), OrderFirst = true)]
    public partial class InitializeChatSystemRuntimeDataSystem : SystemBase
    {
        private ChatSystemRuntimeData _chatSystemRuntimeData;

        protected override void OnCreate()
        {
            base.OnCreate();
            _chatSystemRuntimeData = new(Allocator.Persistent);
            EntityManager.AddComponentData(SystemHandle, _chatSystemRuntimeData);
        }

        protected override void OnUpdate() { }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _chatSystemRuntimeData.Dispose();
        }
    }

    [UpdateInGroup(typeof(ChatBotSystemGroup))]
    public partial class ChatMessageProvidersSystemGroup : ComponentSystemGroup
    { }
}