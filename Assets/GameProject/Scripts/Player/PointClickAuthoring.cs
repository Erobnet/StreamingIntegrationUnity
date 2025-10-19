using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;

namespace GameProject.Player
{
    [ExecuteInEditMode]
    public class PointClickAuthoring : MonoBehaviour
    {
        [FormerlySerializedAs("LayerMask")] [SerializeField] private LayerMask groundLayerMask;
        [SerializeField] private LayerMask actionnableObjectLayerMask;
        [SerializeField] private GameObject TargetMarkerPrefab;
        [SerializeField] private float MarkerOffsetFromGround = .02f;
        [SerializeField, Min(0.25f)] private float MarkerDisapearDistance = 1f;

        private class Baker : Baker<PointClickAuthoring>
        {
            public override void Bake(PointClickAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new PointClickComponentData {
                    TargetMarkerPrefab = authoring.TargetMarkerPrefab,
                    GroundLayer = (uint)(authoring.groundLayerMask.value),
                    MarkerOffsetFromGround = authoring.MarkerOffsetFromGround,
                    MarkerDisapearDistanceSq = authoring.MarkerDisapearDistance * authoring.MarkerDisapearDistance,
                    ActionnableObjectMask = (uint)authoring.actionnableObjectLayerMask.value,
                });
            }
        }
    }

    public struct PointClickComponentData : IComponentData
    {
        public UnityObjectRef<GameObject> TargetMarkerPrefab;
        public float MarkerOffsetFromGround;
        public float MarkerDisapearDistanceSq;
        public uint GroundLayer;
        public uint ActionnableObjectMask;
    }
}