using Unity.Entities;
using UnityEngine;

namespace GameProject.Sound
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    public partial class AudioInputAnalyzerSystem : SystemBase
    {
        private const int _SAMPLE_SIZE = 128;
        private string _listeningAudioDeviceName;
        private AudioClip _recordClip;
        private float[] _spectrum;
        private EntityQuery _deviceSettingsQuery;
        private AudioInputDeviceSettings _audioInputDeviceData;

        protected override void OnCreate()
        {
            base.OnCreate();
            _spectrum = new float[_SAMPLE_SIZE];
            RequireForUpdate<AudioInputDeviceSettings>();
            _deviceSettingsQuery = SystemAPI.QueryBuilder().WithAll<AudioInputDeviceSettings>().Build();
            _deviceSettingsQuery.SetChangedVersionFilter<AudioInputDeviceSettings>();
        }

        protected override void OnUpdate()
        {
            if ( !_deviceSettingsQuery.IsEmpty )
            {
                _audioInputDeviceData = SystemAPI.GetSingleton<AudioInputDeviceSettings>();
                StopListeningIfRecording();
                if ( _audioInputDeviceData.DeviceIndex > -1 )
                {
                    _listeningAudioDeviceName = Microphone.devices[_audioInputDeviceData.DeviceIndex];
                }
                _recordClip = Microphone.Start(_listeningAudioDeviceName, true, 10, _audioInputDeviceData.RecordFrequency);
            }

            int mic_pos = Microphone.GetPosition(_listeningAudioDeviceName) - (_SAMPLE_SIZE + 1);
            if ( mic_pos < 0 )
                return;

            float audioLevel = _audioInputDeviceData.DeviceDefaultAudioLevel;
            bool speaking = false;
            _recordClip.GetData(_spectrum, mic_pos);
            for ( int i = 0; i < _spectrum.Length && !speaking; i++ )
            {
                float peak = _spectrum[i];
                speaking = peak > audioLevel;
            }

            if ( speaking )
            {
                
            }
        }

        private void StopListeningIfRecording()
        {
            if ( Microphone.IsRecording(_listeningAudioDeviceName) )
            {
                Microphone.End(_listeningAudioDeviceName);
            }
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            StopListeningIfRecording();
        }
    }
}