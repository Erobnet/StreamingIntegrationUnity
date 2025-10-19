using System.Diagnostics;
using GameProject.Animation;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace GameProject.Characters
{
    public class RegisterCharacterCreatorAuthoring : MonoBehaviour
    {
        public CharacterCreatorData CharacterCreatorData;

        public unsafe class Baker : Baker<RegisterCharacterCreatorAuthoring>
        {
            public override void Bake(RegisterCharacterCreatorAuthoring authoring)
            {
                DependsOn(authoring.CharacterCreatorData);
                var entity = GetEntity(TransformUsageFlags.None);

                var categories = authoring.CharacterCreatorData.Categories;
                var colorOptionIndices = new NativeList<CharacterColorOptionLength>(categories.Length, Allocator.Temp);
                var blobBuilder = new BlobBuilder(Allocator.Temp);
                ref var blobRoot = ref blobBuilder.ConstructRoot<CharacterCreationSettingsData>();
                var optionPerCategoryArrayBuilder = blobBuilder.Allocate(ref blobRoot.ColorOptionsPerCategory, categories.Length);
                var optionSkinBuilderArray = blobBuilder.Allocate(ref blobRoot.SkinOptionLenghts, categories.Length);

                for ( var index = 0; index < categories.Length; index++ )
                {
                    var category = categories[index];
                    optionSkinBuilderArray[index] = new() {
                        Length = new() {
                            Index = (byte)category.OptionSets.Length
                        }
                    };

                    optionPerCategoryArrayBuilder[index] = new() {
                        StartIndex = colorOptionIndices.Length
                    };

                    foreach ( var libraryTransitionData in category.OptionSets )
                    {
                        colorOptionIndices.Add(new() {
                            Length = new() {
                                Index = (byte)libraryTransitionData.ColorOptions.Length
                            }
                        });
                    }
                }

                var optionContentBuilderArray = blobBuilder.Allocate(ref blobRoot.ColorOptionsLengths, colorOptionIndices.Length);
                UnsafeUtility.MemCpy(optionContentBuilderArray.GetUnsafePtr(), colorOptionIndices.GetUnsafePtr(), sizeof(CharacterColorOptionLength) * colorOptionIndices.Length);

                var colorBlobAssetReference = blobBuilder.CreateBlobAssetReference<CharacterCreationSettingsData>(Allocator.Domain);
                AddComponent(entity, new CharacterCreationComponentData {
                    CharacterCreatorData = authoring.CharacterCreatorData,
                    RuntimeSettings = colorBlobAssetReference
                });
            }
        }
    }

    public struct ColorOptionArrayDescriptor
    {
        public int StartIndex;
    }

    public struct CharacterCreationSettingsData
    {
        public BlobArray<ColorOptionArrayDescriptor> ColorOptionsPerCategory;
        public BlobArray<CharacterColorOptionLength> ColorOptionsLengths;
        public BlobArray<CharacterSwapSkinOptionLength> SkinOptionLenghts;
    }

    public struct CharacterCreationComponentData : IComponentData
    {
        public UnityObjectRef<CharacterCreatorData> CharacterCreatorData;
        public BlobAssetReference<CharacterCreationSettingsData> RuntimeSettings;
    }

    [DebuggerDisplay("{Length}")]
    public struct CharacterColorOptionLength : ICharacterCategoryIndex
    {
        public CharacterOptionIndex Length;

        CharacterOptionIndex ICharacterCategoryIndex.Value {
            get => Length;
            set => Length = value;
        }
    }

    public struct CharacterSwapSkinOptionLength : ICharacterCategoryIndex
    {
        public CharacterOptionIndex Length;

        CharacterOptionIndex ICharacterCategoryIndex.Value {
            get => Length;
            set => Length = value;
        }
    }
}