using Unity.Entities;
using UnityEngine;

namespace GameProject.Sound
{
    public class AudioInputDeviceSettingsAuthoring : MonoBehaviour
    {
        public int DeviceIndex=-1;
        public int RecordFrequency = 441000;
        public float DefaultAudioLevel;

        private class Baker : Baker<AudioInputDeviceSettingsAuthoring>
        {
            public override void Bake(AudioInputDeviceSettingsAuthoring authoring)
            {
                DependsOn(authoring);
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new AudioInputDeviceSettings {
                    DeviceIndex = authoring.DeviceIndex,
                    RecordFrequency = authoring.RecordFrequency,
                    DeviceDefaultAudioLevel = authoring.DefaultAudioLevel,
                });
            }
        }
    }

    public struct AudioInputDeviceSettings : IComponentData
    {
        public float DeviceDefaultAudioLevel;
        public int DeviceIndex;
        public int RecordFrequency; //441000
    }
}