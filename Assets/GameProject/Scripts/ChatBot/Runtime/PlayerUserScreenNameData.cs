using Unity.Collections;
using Unity.Entities;

namespace ChatBot.Runtime
{
    public struct PlayerUserScreenNameData : IComponentData
    {
        public FixedString64Bytes UserScreenName;
    }
}