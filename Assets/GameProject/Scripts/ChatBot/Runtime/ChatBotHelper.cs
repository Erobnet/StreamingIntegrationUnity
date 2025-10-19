using ChatBot.Runtime;
using Drboum.Utilities.Entities;
using Unity.Collections;
using Unity.Entities;

namespace ChatBot.Runtime
{
    public static class ChatBotHelper
    {
        public static ComponentTypeSet GetChatBotUserRootRequiredComponents()
        {
            var componentSet = new FixedList128Bytes<ComponentType>();
            componentSet.AddChatBotUserRootRequiredComponents();
            return new ComponentTypeSet(in componentSet);
        }
    }
}

public static class ChatBotExtensions
{
    public static void BakeChatBotUserRootRequiredComponents(this IBaker baker, Entity entity)
    {
        baker.AddComponent(entity, ChatBotHelper.GetChatBotUserRootRequiredComponents());
        baker.SetComponentEnabled<HasNewTextTag>(entity, false);
    }

    public static void AddChatBotUserRootRequiredComponents(this EntityManager em, Entity entity)
    {
        em.AddComponent(entity, ChatBotHelper.GetChatBotUserRootRequiredComponents());
    }

    public static void AddChatBotUserRootRequiredComponents(this EntityCommandBuffer ecb, Entity entity)
    {
        ecb.AddComponent(entity, ChatBotHelper.GetChatBotUserRootRequiredComponents());
    }

    public static void AddChatBotUserRootRequiredComponents(this ref FixedList128Bytes<ComponentType> componentSet)
    {
        componentSet.Add(ComponentType.ReadWrite<ChatUserComponent>());
        componentSet.Add(ComponentType.ReadWrite<InitializeEntityTag>());
        componentSet.Add(ComponentType.ReadWrite<HasNewTextTag>());
    }
}