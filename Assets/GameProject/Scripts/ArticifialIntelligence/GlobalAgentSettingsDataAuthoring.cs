using Unity.Entities;
using UnityEngine;

namespace GameProject.ArticifialIntelligence
{
    public class GlobalAgentSettingsDataAuthoring : MonoBehaviour
    {
        public float TimeOutToGoOutWhenLeavingGameInSeconds = 10;
        public float WaitTimeBeforeNewDestinationInSeconds = 20;
        public float IdleDurationInSeconds = 10;

        public class Baker : Baker<GlobalAgentSettingsDataAuthoring>
        {
            public override void Bake(GlobalAgentSettingsDataAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new GlobalAgentSettingsData {
                    WaitTimeBeforeNewDestination = authoring.WaitTimeBeforeNewDestinationInSeconds,
                    IdleDuration = authoring.IdleDurationInSeconds,
                    TimeOutToGoOutWhenLeavingGameInSeconds = authoring.TimeOutToGoOutWhenLeavingGameInSeconds,
                });
            }
        }
    }

    public struct GlobalAgentSettingsData : IComponentData
    {
        public float TimeOutToGoOutWhenLeavingGameInSeconds;
        public float WaitTimeBeforeNewDestination;
        public float IdleDuration;
    }
}