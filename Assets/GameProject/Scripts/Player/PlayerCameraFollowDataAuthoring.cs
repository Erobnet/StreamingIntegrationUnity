using Unity.Entities;
using UnityEngine;

namespace GameProject.Player
{
    public class PlayerCameraFollowDataAuthoring : MonoBehaviour
    {
        public LayerMask FollowCinemachineVCameraGameObjectMask;

        public class PlayerCameraFollowDataBaker : Baker<PlayerCameraFollowDataAuthoring>
        {
            public override void Bake(PlayerCameraFollowDataAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new PlayerCameraFollowData {
                    FollowCinemachineLayerMask = authoring.FollowCinemachineVCameraGameObjectMask
                });
            }
        }
    }

    public struct PlayerCameraFollowData : IComponentData
    {
        public int FollowCinemachineLayerMask;
    }
}