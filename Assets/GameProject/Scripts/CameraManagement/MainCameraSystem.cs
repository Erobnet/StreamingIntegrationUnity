using GameProject.Inputs;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GameProject.CameraManagement
{
    public unsafe partial class MainCameraSystem : SystemBase
    {
        private Entity _mainCameraEntity;
        private GameInputsAsset _gameInputsAsset;

        protected override void OnCreate()
        {
            base.OnCreate();
            _mainCameraEntity = EntityManager.CreateEntity(EntityManager.CreateArchetype(stackalloc ComponentType[] {
                ComponentType.ReadOnly<MainCameraTag>(),
                ComponentType.ReadOnly<RayData>(),
                ComponentType.ReadOnly<PositionRotationData>(),
            }));
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            _gameInputsAsset = World.GetOrCreateSystemManaged<InputSystemCoordinator>().GameInputsAsset;
        }

        protected override void OnUpdate()
        {
            if ( !MainCameraComponent.Main )
                return;

            var cursorPosition = _gameInputsAsset.UI.Point.ReadValue<Vector2>();
            var cameraRayData = new RayData {
                Value = MainCameraComponent.Main.ScreenPointToRay(new Vector3(cursorPosition.x, cursorPosition.y, 0))
            };
            EntityManager.SetComponentData(_mainCameraEntity, cameraRayData);
            Transform mainTransform = MainCameraComponent.Main.transform;
            EntityManager.SetComponentData(_mainCameraEntity,
                new PositionRotationData {
                    Position = mainTransform.position,
                    Rotation = mainTransform.rotation
                });
        }
    }

    public struct MainCameraTag : IComponentData
    { }

    public struct RayData : IComponentData
    {
        public Ray Value;
    }

    public struct PositionRotationData : IComponentData
    {
        public float3 Position;
        public quaternion Rotation;
    }
}