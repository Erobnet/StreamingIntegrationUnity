using GameProject.Animation;
using Unity.Cinemachine;
using Unity.Entities;
using UnityEngine.SceneManagement;

namespace GameProject.Player
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(CharacterAnimatorHybridSpawnerSystem))] //spawns the animator we want to track
    internal partial class LinkCameraToLocalPlayerSystem : SystemBase
    {
        private CinemachineCamera _cinemachineCamera;
        private PlayerCameraFollowData _playerCameraFollowData;

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<PlayerCameraFollowData>();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            _playerCameraFollowData = SystemAPI.GetSingleton<PlayerCameraFollowData>();
        }

        protected override void OnUpdate()
        {
#if UNITY_EDITOR
            _playerCameraFollowData = SystemAPI.GetSingleton<PlayerCameraFollowData>();
#endif
            if ( !_cinemachineCamera )
            {
                _cinemachineCamera = SceneManager.GetActiveScene().FindFirstInstancesInScene<CinemachineCamera>(_playerCameraFollowData.FollowCinemachineLayerMask);

                _cinemachineCamera.gameObject.CheckSetActive(false);
            }

            if ( !_cinemachineCamera )
                return;

            foreach ( var spawnInstance
                     in SystemAPI.Query<CharacterAnimatorSpawnInstance>()
                         .WithAll<LocalPlayerTag>()
                         .WithChangeFilter<CharacterAnimatorSpawnInstance>() )
            {
                _cinemachineCamera.Follow = spawnInstance.Value.transform;
                _cinemachineCamera.gameObject.CheckSetActive(true);
            }
        }
    }
}