using GameProject.Characters;
using GameProject.GameWorldData;
using ProjectDawn.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace GameProject.Animation
{
    /// <summary>
    /// System responsible to update the character animation state using the character state itself and also apply the personalization options for the characters on the renderers 
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial struct CharacterManagedUpdateSystem : ISystem
    {
        private EntityQuery _applySkinQuery;
        private EntityQuery _applySkinColorQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _applySkinQuery = SystemAPI.QueryBuilder()
                .WithAny<PersistentSkinOptionApply, SkinOptionOverrideApply>()
                .Build();

            _applySkinColorQuery = SystemAPI.QueryBuilder()
                .WithAll<PersistentSkinColorOptionApply>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach ( var (characterAnimatorSpawnInstance, parent, ecsTransform)
                     in SystemAPI.Query<CharacterAnimatorSpawnInstance, RefRO<Parent>, RefRO<LocalToWorld>>() )
            {
                var characterAnimator = characterAnimatorSpawnInstance.Value;
                var agentBody = SystemAPI.GetComponent<AgentBody>(parent.ValueRO.Value);
                var agentVelocity = agentBody.Velocity;
                bool isSitting = SystemAPI.GetComponent<CharacterGameplayStateComponent>(parent.ValueRO.Value).Is(CharacterGameplayState.Sitting);
                var facingDirection = default(float2); // forward
                bool isMoving = math.lengthsq(agentVelocity) > 0.01f;
                if ( isMoving )
                {
                    float3 agentVelocityFlatten = agentVelocity.FlattenNormalize();
                    facingDirection.x = agentVelocityFlatten.x;
                    facingDirection.y = agentVelocityFlatten.z;
                }
                if ( isSitting )
                {
                    switch ( SystemAPI.GetComponent<SittableData>(SystemAPI.GetComponent<TargetEntity>(parent.ValueRO.Value).Target).Facing )
                    {
                        case CharacterFacing.Forward:
                            facingDirection = new float2(0, 1);
                            break;
                        case CharacterFacing.Left:
                            facingDirection = new float2(-1, 0);
                            break;
                        case CharacterFacing.Right:
                            facingDirection = new float2(1, 0);
                            break;
                    }
                }
                characterAnimator.UpdateAnimators(agentVelocity, facingDirection, isSitting);
                characterAnimatorSpawnInstance.CachedTransform.position = (ecsTransform.ValueRO.Position);
            }

            if ( !SystemAPI.TryGetSingleton<CharacterCreationComponentData>(out var characterCreatorData) )
                return;

            var persistentCategories = characterCreatorData.CharacterCreatorData.Value.Categories;
            if ( !_applySkinQuery.IsEmpty )
            {
                foreach ( var (characterAnimatorSpawnInstance
                             , characterSwapSkinIndices, enabledPersistentSkinOptionApplyRW
                             , tempSkinOptionApplies, enabledtempSkinOptionApplyRW)
                         in SystemAPI.Query<CharacterAnimatorSpawnInstance
                                 , DynamicBuffer<PersistentSkinOptionApply>, EnabledRefRW<PersistentSkinOptionApply>
                                 , DynamicBuffer<SkinOptionOverrideApply>, EnabledRefRW<SkinOptionOverrideApply>>()
                             .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState) )
                {
                    bool persistentSkinChanged = enabledPersistentSkinOptionApplyRW.ValueRO;
                    bool skinOverrideHasChanged = enabledtempSkinOptionApplyRW.ValueRO;
                    if ( persistentSkinChanged || skinOverrideHasChanged )
                    {
                        ApplySkinOptions(persistentCategories, characterSwapSkinIndices, enabledPersistentSkinOptionApplyRW, characterAnimatorSpawnInstance.Value);
                        ApplySkinOptionsOverride(tempSkinOptionApplies, characterSwapSkinIndices, enabledtempSkinOptionApplyRW, characterAnimatorSpawnInstance.Value);
                    }
                }
            }

            if ( !_applySkinColorQuery.IsEmpty )
            {
                foreach ( var (characterAnimatorSpawnInstance
                             , persistentSkinOptionApplies
                             , persistentSkinColorOptionApplies, enabledPersistentSkinColorOptionApplyRW)
                         in SystemAPI.Query<CharacterAnimatorSpawnInstance
                                 , DynamicBuffer<PersistentSkinOptionApply>
                                 , DynamicBuffer<PersistentSkinColorOptionApply>, EnabledRefRW<PersistentSkinColorOptionApply>>()
                             .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState) )
                {
                    bool skinColorHasChanged = enabledPersistentSkinColorOptionApplyRW.ValueRO;
                    if ( skinColorHasChanged )
                    {
                        ApplySkinColorOptions(persistentCategories, persistentSkinOptionApplies.AsNativeArray(), persistentSkinColorOptionApplies, enabledPersistentSkinColorOptionApplyRW, characterAnimatorSpawnInstance.Value);
                    }
                }
            }
        }

        private void ApplySkinColorOptions<TApplySkinColor, TApplySkin>(CharacterAnatomyCategory[] transitionCategories, NativeArray<TApplySkin> characterSwapIndices, DynamicBuffer<TApplySkinColor> characterColorOptionIndices, EnabledRefRW<TApplySkinColor> enabledRefRW, CharacterAnimationRoot characterAnimationRoot)
            where TApplySkinColor : unmanaged, IEnableableComponent, ICharacterCategoryIndex
            where TApplySkin : unmanaged, IEnableableComponent, ICharacterCategoryIndex
        {
            int length = math.min(characterSwapIndices.Length, transitionCategories.Length);
            for ( var index = 0; index < length; index++ )
            {
                var persistentSkinOptionApply = characterSwapIndices[index];
                var categoryTarget = transitionCategories[index];
                var optionSetChoice = categoryTarget.OptionSets[persistentSkinOptionApply.Value];

                byte colorIndex = characterColorOptionIndices[index].Value;
                if ( !CollectionCustomHelper.IsIndexInRange(colorIndex, optionSetChoice.ColorOptions.Length) )
                    continue;

                Material materialOption = optionSetChoice.ColorOptions[colorIndex].MaterialOption;

                foreach ( var applyColorPart in categoryTarget.ApplyColorData )
                {
                    characterAnimationRoot.SetSpriteRendererMaterial(applyColorPart.SlotID, materialOption);
                }
            }
            enabledRefRW.ValueRW = false;
        }

        private static void ApplySkinOptions<TApplySkin>(CharacterAnatomyCategory[] transitionCategories, DynamicBuffer<TApplySkin> characterSwapIndices, EnabledRefRW<TApplySkin> enabledRefRW, CharacterAnimationRoot characterAnimationRoot)
            where TApplySkin : unmanaged, IEnableableComponent, ICharacterCategoryIndex
        {
            int length = math.min(characterSwapIndices.Length, transitionCategories.Length);
            for ( var index = 0; index < length; index++ )
            {
                var persistentSkinOptionApply = characterSwapIndices[index];
                var categoryTarget = transitionCategories[index];
                var optionSetChoice = categoryTarget.OptionSets[persistentSkinOptionApply.Value];
                foreach ( var partSlot in optionSetChoice.SpriteLibraryAssets_Sets )
                {
                    characterAnimationRoot.SetSpriteLibrarySlot(partSlot);
                }
            }
            enabledRefRW.ValueRW = false;
            characterAnimationRoot.ResyncAnimation();
        }

        private static void ApplySkinOptionsOverride<TApplySkin>(DynamicBuffer<SkinOptionOverrideApply> skinOverride
            , DynamicBuffer<PersistentSkinOptionApply> persistentSkinOptionApplies
            , EnabledRefRW<TApplySkin> enabledRefRW, CharacterAnimationRoot characterAnimationRoot)
            where TApplySkin : unmanaged, IEnableableComponent
        {
            int length = skinOverride.Length;
            for ( var index = 0; index < length; index++ )
            {
                var persistentSkinOptionApply = skinOverride[index];
                var optionSetChoice = persistentSkinOptionApply.CharacterTransitionWrapper.Value;

                foreach ( var spriteAssetSource in optionSetChoice.SpriteLibraryAssetSets )
                {
                    var part = new PartSlot {
                        PartAsset = spriteAssetSource.GetPartLibraryAsset(in persistentSkinOptionApplies),
                        SlotID = spriteAssetSource.TargetSlotID
                    };

                    characterAnimationRoot.SetSpriteLibrarySlot(part);
                }

                var materialOption = persistentSkinOptionApply.SelectedColor.Value;
                if ( !materialOption )
                    continue;

                foreach ( var applyColorPart in optionSetChoice.CharacterTransitionDefinition.ApplyColorData )
                {
                    characterAnimationRoot.SetSpriteRendererMaterial(applyColorPart.SlotID, materialOption);
                }
            }
            enabledRefRW.ValueRW = false;
            characterAnimationRoot.ResyncAnimation();
        }
    }
}