using System;
using System.Runtime.CompilerServices;
using GameProject.Animation;
using GameProject.Characters;
using GameProject.Player;
using ProjectDawn.Navigation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.SceneManagement;

namespace GameProject
{
    internal partial class CustomizationUISystem : SystemBase
    {
        protected static readonly int CategoryCount = CharacterCreationExtensions.CategoryCount;

        protected GlobalUIController GlobalUIController;
        protected NativeArray<NativeReference<ChangeTracker<int>>> SkinIndexChangeTrackers;
        protected NativeArray<NativeReference<ChangeTracker<int>>> SkinColorIndexChangeTrackers;

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<GameplaySettingsData>();
            RequireForUpdate<LocalPlayerTag>();
            RequireForUpdate<CharacterAnimatorSpawnInstance>();
            SkinIndexChangeTrackers = new(CategoryCount, Allocator.Persistent);
            SkinColorIndexChangeTrackers = new(CategoryCount, Allocator.Persistent);
            for ( int i = 0; i < CategoryCount; i++ )
            {
                SkinIndexChangeTrackers[i] = new(default, Allocator.Persistent);
                SkinColorIndexChangeTrackers[i] = new(default, Allocator.Persistent);
            }
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            GlobalUIController = SceneManager.GetActiveScene().FindFirstInstancesInScene<GlobalUIController>();
            if ( GlobalUIController )
            {
                GlobalUIController.SyncCustomizationUIFieldsWithLocalPlayerCharacter(SkinIndexChangeTrackers, SkinColorIndexChangeTrackers);
            }
        }

        protected override void OnUpdate()
        {
            if ( !GlobalUIController || !GlobalUIController.IsCharacterCreatorOpen )
                return;

            var gameplaySettingsData = SystemAPI.GetSingleton<GameplaySettingsData>();
            foreach ( var transform
                     in SystemAPI.Query<LocalTransform>()
                         .WithAll<LocalPlayerTag, AgentBody>() )
            {
                var mirrorCameraTransform = GlobalUIController.MirrorCamera.transform;
                mirrorCameraTransform.position = (transform.Position + math.mul(mirrorCameraTransform.rotation, gameplaySettingsData.OffsetFromPlayerWithMirrorCamera));
            }

            foreach ( var (persistentSkinOptionApply, persistentSkinColorOptionApply, enabledRefSkinRW, enabledRefSkinColorRW)
                     in SystemAPI.Query<DynamicBuffer<PersistentSkinOptionApply>, DynamicBuffer<PersistentSkinColorOptionApply>, EnabledRefRW<PersistentSkinOptionApply>, EnabledRefRW<PersistentSkinColorOptionApply>>()
                         .WithAll<CharacterAnimatorSpawnInstance, LocalPlayerTag>()
                         .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState) )
            {
                var skinIndexChanged = false;
                var skinColorIndexChanged = false;
                for ( int i = 0; i < SkinIndexChangeTrackers.Length; i++ )
                {
                    CheckSkinIndexChanged(ref skinIndexChanged, SkinIndexChangeTrackers, persistentSkinOptionApply, i);
                    CheckSkinIndexChanged(ref skinColorIndexChanged, SkinColorIndexChangeTrackers, persistentSkinColorOptionApply, i);
                }

                if ( skinIndexChanged )
                    enabledRefSkinRW.ValueRW = true;

                if ( skinColorIndexChanged )
                    enabledRefSkinColorRW.ValueRW = true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckSkinIndexChanged<TApply>(ref bool skinIndexChanged, NativeArray<NativeReference<ChangeTracker<int>>> skinIndexChangeTracker, DynamicBuffer<TApply> persistentOptionApply, int i)
            where TApply : unmanaged, IBufferElementData, ICharacterCategoryIndex
        {
            ref var changeTracker = ref skinIndexChangeTracker.ReadElementAsRef(i).AsRef();
            if ( changeTracker.UpdateHasChanged() )
            {
                persistentOptionApply.ElementAt(i).Value = changeTracker.Value;
                skinIndexChanged = true;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            for ( var index = 0; index < SkinIndexChangeTrackers.Length; index++ )
            {
                ref var skinIndexChangeTracker = ref SkinIndexChangeTrackers.ReadElementAsRef(index);
                ref var skinColorIndexChangeTracker = ref SkinColorIndexChangeTrackers.ReadElementAsRef(index);
                skinIndexChangeTracker.Dispose();
                skinColorIndexChangeTracker.Dispose();
            }
            SkinIndexChangeTrackers.Dispose();
            SkinColorIndexChangeTrackers.Dispose();
        }
    }
}