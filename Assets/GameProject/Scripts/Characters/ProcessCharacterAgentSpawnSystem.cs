using System;
using ChatBot.Runtime;
using Drboum.Utilities.Entities;
using GameProject.Animation;
using GameProject.ArticifialIntelligence;
using GameProject.Persistence;
using GameProject.Persistence.CommonData;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Profiling;
using Random = Unity.Mathematics.Random;

namespace GameProject.Characters
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(CharacterAnimatorHybridSpawnerSystem))]
    public unsafe partial struct ProcessCharacterAgentSpawnSystem : ISystem
    {
        private EntityQuery _newViewerCharacterRootQuery;
        private EntityQuery _characterCreationDataQuery;
        private NativeReference<Random> _randomRef;

        public void OnCreate(ref SystemState state)
        {
            _randomRef = new NativeReference<Random>(new Random((uint)DateTime.Now.Ticks), Allocator.Persistent);
            _newViewerCharacterRootQuery = SystemAPI.QueryBuilder()
                .WithAll<InitializeEntityTag, AgentBrainStateComponent, CharacterHierarchyHubData, ChatUserComponent, GameCurrency>()
                .Build();

            _characterCreationDataQuery = SystemAPI.QueryBuilder()
                .WithAll<CharacterCreationComponentData>()
                .Build();
            
            state.RequireForUpdate(_newViewerCharacterRootQuery);
            state.RequireForUpdate(_characterCreationDataQuery);        
            state.RequireForUpdate<PersistenceSystem.PersistenceCachedData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var characterPersistence = SystemAPI.GetSingleton<PersistenceSystem.PersistenceCachedData>();
            var characterCreatorData = _characterCreationDataQuery.GetSingleton<CharacterCreationComponentData>();
            var characterCreationSettingsReference = characterCreatorData.RuntimeSettings;
            int categoryCount = characterCreationSettingsReference.Value.SkinOptionLenghts.Length;
            var spawnOptionsBuffer = new NativeList<CharacterOptionIndex>(categoryCount, Allocator.TempJob);
            spawnOptionsBuffer.Length = categoryCount;

            new ProcessCharacterViewerSpawnJob {
                Random = _randomRef,
                CharacterCreationSettingsRef = characterCreationSettingsReference,
                SpawnOptionsBuffer = spawnOptionsBuffer,
                CharacterColorOptionsLookup = SystemAPI.GetBufferLookup<PersistentSkinColorOptionApply>(),
                CharacterSwapOptionIndexLookup = SystemAPI.GetBufferLookup<PersistentSkinOptionApply>(),
                PersistenceCachedData = characterPersistence,
            }.Run(_newViewerCharacterRootQuery);
            spawnOptionsBuffer.Dispose();
        }

        [BurstCompile]
        public partial struct ProcessCharacterViewerSpawnJob : IJobEntity
        {
            private static readonly ProfilerMarker _ExecuteMarker = new(nameof(ProcessCharacterViewerSpawnJob) + "Marker");

            public NativeReference<Random> Random;
            [ReadOnly] public BlobAssetReference<CharacterCreationSettingsData> CharacterCreationSettingsRef;
            public NativeList<CharacterOptionIndex> SpawnOptionsBuffer;
            public BufferLookup<PersistentSkinOptionApply> CharacterSwapOptionIndexLookup;
            public BufferLookup<PersistentSkinColorOptionApply> CharacterColorOptionsLookup;
            [NativeDisableContainerSafetyRestriction] public PersistenceSystem.PersistenceCachedData PersistenceCachedData;

            void Execute(ref GameCurrency currency, ref AgentBrainStateComponent brainState, in CharacterHierarchyHubData hierarchyHub, in ChatUserComponent chatUserComponent)
            {
                _ExecuteMarker.Begin();
                Entity animationRootEntity = hierarchyHub.AnimationRoot;
                SkinOptions skinOption = default;
                SkinColorOptions colorOption = default;
                if ( PersistenceCachedData.CharacterDataLookup.TryGetValueAsRef(in chatUserComponent.UserId, ref currency, ref skinOption, ref colorOption) )
                {
                    ApplyOptionIndices(animationRootEntity, ref skinOption.Indices, CharacterSwapOptionIndexLookup);
                    ApplyOptionIndices(animationRootEntity, ref colorOption.Indices, CharacterColorOptionsLookup);
                }
                else
                {
                    Random randomValue = Random.Value;
                    var characterOptionIndicesTemp = SpawnOptionsBuffer.AsArray();
                    RollCharacterAppearance(characterOptionIndicesTemp, hierarchyHub, CharacterSwapOptionIndexLookup, CharacterColorOptionsLookup, CharacterCreationSettingsRef, ref randomValue);
                    Random.Value = randomValue;
                }
                brainState.AgentBrainState = AgentBrainState.MoveToNextRandomDestination;
                _ExecuteMarker.End();
            }
        }

        public static void ApplyOptionIndices<TApply>(Entity animationRootEntity, ref FixedList32Bytes<byte> characterDataSkinIndices, BufferLookup<TApply> characterSwapOptionIndexLookup)
            where TApply : unmanaged, IBufferElementData, ICharacterCategoryIndex
        {
            if ( characterDataSkinIndices.IsEmpty )
                return;

            var characterSwapOptionTempBuffer = UnsafeUtility.AddressOf(ref characterDataSkinIndices.ElementAt(0));
            ApplyCharacterOptions((TApply*)characterSwapOptionTempBuffer, characterDataSkinIndices.Length, characterSwapOptionIndexLookup, animationRootEntity);
        }

        public static void ApplyOptionIndices<TApply>(Entity animationRootEntity, ref FixedList64Bytes<byte> characterDataSkinIndices, BufferLookup<TApply> characterSwapOptionIndexLookup)
            where TApply : unmanaged, IBufferElementData, ICharacterCategoryIndex
        {
            if ( characterDataSkinIndices.IsEmpty )
                return;

            var characterSwapOptionTempBuffer = UnsafeUtility.AddressOf(ref characterDataSkinIndices.ElementAt(0));
            ApplyCharacterOptions((TApply*)characterSwapOptionTempBuffer, characterDataSkinIndices.Length, characterSwapOptionIndexLookup, animationRootEntity);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _randomRef.Dispose();
        }

        public static void RollRandomCharacterSkin(Entity animationRootEntity,
            ref BlobArray<CharacterSwapSkinOptionLength> characterOptionsLength,
            NativeArray<PersistentSkinOptionApply> characterOptionTempBuffer,
            BufferLookup<PersistentSkinOptionApply> characterOptionIndexLookup, ref Random randomValue)
        {
            for ( var optionCategoryIndex = 0; optionCategoryIndex < characterOptionsLength.Length; optionCategoryIndex++ )
            {
                var skinOption = characterOptionsLength[optionCategoryIndex];
                characterOptionTempBuffer[optionCategoryIndex] = new() {
                    Value = GetRandomIndex(ref randomValue, skinOption)
                };
            }
            var sourcePtr = (PersistentSkinOptionApply*)characterOptionTempBuffer.GetUnsafeReadOnlyPtr();
            ApplyCharacterOptions(sourcePtr, characterOptionsLength.Length, characterOptionIndexLookup, animationRootEntity);
        }

        public static void RollRandomCharacterColors(Entity animationRootEntity, BlobAssetReference<CharacterCreationSettingsData> characterCreationSettingsRef, NativeArray<PersistentSkinOptionApply> characterSkinOptionIndices, NativeArray<CharacterOptionIndex> optionIndicesTemp, BufferLookup<PersistentSkinColorOptionApply> characterColorOptionIndexLookup, ref Random randomValue)
        {
            ref var colorOptionDescription = ref characterCreationSettingsRef.Value;
            for ( var optionCategoryIndex = 0; optionCategoryIndex < colorOptionDescription.ColorOptionsPerCategory.Length; optionCategoryIndex++ )
            {
                var descriptor = colorOptionDescription.ColorOptionsPerCategory[optionCategoryIndex];
                int lenghtIndexForSkinCategory = descriptor.StartIndex + characterSkinOptionIndices[optionCategoryIndex].Value.Index;
                var lengthOption = colorOptionDescription.ColorOptionsLengths[lenghtIndexForSkinCategory];
                optionIndicesTemp[optionCategoryIndex] = GetRandomIndex(ref randomValue, lengthOption);
            }

            var sourcePtr = (PersistentSkinColorOptionApply*)optionIndicesTemp.GetUnsafeReadOnlyPtr();
            ApplyCharacterOptions(sourcePtr, colorOptionDescription.ColorOptionsPerCategory.Length, characterColorOptionIndexLookup, animationRootEntity);
        }

        public static void RollCharacterAppearance(NativeArray<CharacterOptionIndex> optionIndicesTempArray, CharacterHierarchyHubData charHub, BufferLookup<PersistentSkinOptionApply> characterSwapSkinLookup, BufferLookup<PersistentSkinColorOptionApply> colorOptionIndexLookup, BlobAssetReference<CharacterCreationSettingsData> characterCreationSettingsRef, ref Random systemRandom)
        {
            var skinOptionAppliesTemp = optionIndicesTempArray.Reinterpret<PersistentSkinOptionApply>();
            RollRandomCharacterSkin(charHub.AnimationRoot, ref characterCreationSettingsRef.Value.SkinOptionLenghts, skinOptionAppliesTemp, characterSwapSkinLookup, ref systemRandom);
            RollRandomCharacterColors(charHub.AnimationRoot, characterCreationSettingsRef, skinOptionAppliesTemp, optionIndicesTempArray, colorOptionIndexLookup, ref systemRandom);
        }

        private static void ApplyCharacterOptions<TApply>(TApply* characterSwapOptionTempBuffer, int count, BufferLookup<TApply> characterSwapOptionIndexLookup, Entity animationRootEntity)
            where TApply : unmanaged, IBufferElementData, ICharacterCategoryIndex
        {
            var applyOptionsBuffer = characterSwapOptionIndexLookup[animationRootEntity];
            applyOptionsBuffer.Length = count;
            UnsafeUtility.MemCpy(applyOptionsBuffer.GetUnsafePtr(), characterSwapOptionTempBuffer, count * sizeof(TApply));
            characterSwapOptionIndexLookup.SetBufferEnabled(animationRootEntity, true);
        }

        private static CharacterOptionIndex GetRandomIndex<TLength>(ref Random random, TLength skinOption)
            where TLength : unmanaged, ICharacterCategoryIndex
        {
            return new CharacterOptionIndex {
                Index = (byte)random.NextInt(skinOption.Value)
            };
        }
    }
}