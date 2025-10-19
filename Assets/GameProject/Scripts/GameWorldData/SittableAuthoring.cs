using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GameProject.GameWorldData
{
    public class SittableAuthoring : MonoBehaviour
    {
        [SerializeField] private Transform sitPoint;
        [SerializeField] private CharacterFacing characterFacing;

        private void OnValidate()
        {
            if ( sitPoint && !sitPoint.IsChildOf(transform) )
            {
                Debug.LogError($"the {nameof(sitPoint)} {sitPoint.name} must be a children of {name}");
            }
        }

        private class Baker : Baker<SittableAuthoring>
        {
            public override void Bake(SittableAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
                AddComponent(entity, new SittableData {
                    SitPointOffset = authoring.sitPoint.position - authoring.transform.position,
                    Facing = authoring.characterFacing
                });
                AddComponent<HasVacancy>(entity);
            }
        }
    }

    public enum CharacterFacing
    {
        Forward,
        Left,
        Right,
    }

    public struct HasVacancy : IComponentData, IEnableableComponent
    {
        public Entity Entity;
    }

    public struct SittableData : IComponentData
    {
        public float3 SitPointOffset;
        public CharacterFacing Facing;
    }
}