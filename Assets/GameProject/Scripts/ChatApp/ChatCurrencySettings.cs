using Unity.Entities;
using UnityEngine;

namespace GameProject.ChatApp
{
    public struct ChatCurrencySettings : IComponentData
    {
        public float ViewerRateMoneyPerSeconds;
        public float UpdateIntervalInSeconds;
    }

}