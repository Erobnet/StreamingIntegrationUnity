using Drboum.Utilities.Entities;
using GameProject.Persistence.CommonData;
using Unity.Assertions;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GameProject.GameWorldData
{
    public class CoffeeShopAuthoring : MonoBehaviour
    {
        public Transform QueueStartPoint;
        public float ServingDuration = 5;
        public float ServingAreaSize = 1.5f;
        
        private class Baker : Baker<CoffeeShopAuthoring>
        {
            public override void Bake(CoffeeShopAuthoring authoring)
            {
                DependsOn(authoring.QueueStartPoint);
                DependsOn(authoring.transform);

                if ( this.EnsureObjectIsNotNull(authoring.QueueStartPoint, authoring, nameof(authoring.QueueStartPoint), nameof(CoffeeShopAuthoring)) )
                    return;

                Assert.IsTrue(authoring.QueueStartPoint.IsChildOf(authoring.transform));

                var entity = GetEntity(authoring, TransformUsageFlags.Renderable);

                Vector3 startQueuePositionOffset = authoring.QueueStartPoint.localPosition;
                startQueuePositionOffset.Scale(authoring.transform.lossyScale);
                AddComponent(entity, new CoffeeShopData {
                    StartQueuePositionOffset = startQueuePositionOffset,
                    CoffeeServingDuration = authoring.ServingDuration,
                    ServingAreaSize = authoring.ServingAreaSize
                });
                AddBuffer<QueueLine>(entity);
            }
        }
    }

    public struct CoffeeShopData : IComponentData, IEnableableComponent
    {
        public float3 StartQueuePositionOffset;
        public float CoffeeServingDuration;
        public float ServingAreaSize;
        public float CurrentSpentTimeOnCustomer;
    }

    public struct QueueLine : IBufferElementData, IEnableableComponent
    {
        public Entity AgentEntity;
        public AABBComponent AgentReservedSpace;
    }
}