using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

namespace GameProject
{
    enum NavigationUsage
    {
        Navigeable,
        Forbidden,
        PlayableTerrainBounds
    }

    [RequireComponent(typeof(UnityEngine.BoxCollider))]
    public class NavigationAreaAuthoring : MonoBehaviour
    {
        [SerializeField] private TransformUsageFlags transformUsage = TransformUsageFlags.ManualOverride;
        [SerializeField] private NavigationUsage navigationUsage = NavigationUsage.Navigeable;
        [SerializeField, HideInInspector] private UnityEngine.BoxCollider _boxCollider;
        [SerializeField] private bool isBakingCollider = true;
#if UNITY_EDITOR
        private void OnValidate()
        {
            if ( !_boxCollider )
            {
                TryGetComponent(out _boxCollider);
                this.SetDirtySafe();
            }
            if ( isBakingCollider && _boxCollider.enabled )
            {
                _boxCollider.enabled = false;
                _boxCollider.SetDirtySafe();
            }
        }
#endif

        private class Baker : Baker<NavigationAreaAuthoring>
        {
            public override void Bake(NavigationAreaAuthoring authoring)
            {
                DependsOn(authoring.transform);
                DependsOn(authoring._boxCollider);
                var entity = GetEntity(authoring.transformUsage);
                var boxCollider = authoring._boxCollider;
                Transform colliderTransform = boxCollider.transform;
                float3 size = new float3(colliderTransform.lossyScale.x * boxCollider.size.x, colliderTransform.lossyScale.y * boxCollider.size.y, colliderTransform.lossyScale.z * boxCollider.size.z);
                Vector3 halfSize = size * .5f;
                var boxColliderBounds = new Aabb {
                    Min = (colliderTransform.position + boxCollider.center) - halfSize
                };
                boxColliderBounds.Max = boxColliderBounds.Min + size;
                switch ( authoring.navigationUsage )
                {
                    case NavigationUsage.PlayableTerrainBounds:
                        BakeNavigeableArea(entity, boxColliderBounds);
                        AddComponent<MainTerrainAreaDataTag>(entity);
                        break;
                    case NavigationUsage.Navigeable:
                        BakeNavigeableArea(entity, boxColliderBounds);
                        break;
                    case NavigationUsage.Forbidden:
                        AddComponent(entity, new ForbiddenNavigationAreaData {
                            Bounds = new NavigationAreaData { Value = boxColliderBounds }
                        });
                        break;
                }
                if ( authoring.isBakingCollider && authoring.IsPrefabInSubSceneContext() )
                {
                    boxCollider.enabled = false;
                }
            }

            private void BakeNavigeableArea(Entity entity, Aabb boxColliderBounds)
            {
                AddComponent(entity, new NavigationAreaData { Value = boxColliderBounds });
            }
        }
    }

    public struct ForbiddenNavigationAreaData : IComponentData
    {
        public NavigationAreaData Bounds;
    }

    public struct NavigationAreaData : IComponentData
    {
        public Aabb Value;
    }

    public struct MainTerrainAreaDataTag : IComponentData
    { }

}