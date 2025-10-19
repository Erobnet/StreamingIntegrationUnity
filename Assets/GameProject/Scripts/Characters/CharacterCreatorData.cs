using System;
using System.Linq;
using GameProject.Animation;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.U2D.Animation;

namespace GameProject.Characters
{
    [CreateAssetMenu(fileName = nameof(CharacterCreatorData), menuName = nameof(GameProject) + "/" + nameof(CharacterCreatorData))]
    public class CharacterCreatorData : ScriptableObject
    {
        [Tooltip("Character Apparence category settings, will be saved and restored across sessions")]
        public CharacterAnatomyCategory[] Categories = Array.Empty<CharacterAnatomyCategory>();
        public CharacterAnatomyCategory this[int index] => Categories[index];
        public CharacterAnatomyCategory this[CategoryOptionsIndex optionsIndex] => Categories[optionsIndex.GetIndex()];

        private void OnValidate()
        {
            if ( Categories.Length != CharacterCreationExtensions.CategoryCount )
            {
                Categories = Categories.CreateResizedCopy(CharacterCreationExtensions.CategoryCount);
                this.SetDirtySafe();
            }
        }
    }

    public enum CategoryOptionsIndex : byte
    {
        None = 0,
        Base = 1,
        Eyes = 2,
        Nose = 3,
        Cheek = 4,
        Top = 5,
    }

    [Serializable]
    public class CharacterAnatomyCategory
    {
        [FormerlySerializedAs("DisplayName")] public string CategoryDisplayName;
        public ColorApplyPartData[] ApplyColorData;
        public LibraryTransitionData[] OptionSets;
    }

    [Serializable]
    public struct ColorApplyPartData
    {
        public CharacterPartSlotID SlotID;
    }

    [Serializable]
    public class LibraryTransitionData
    {
        public string DisplayName;
        public PartSlot[] SpriteLibraryAssets_Sets;
        public ColorOptionsData[] ColorOptions;
    }

    [Serializable]
    public class ColorOptionsData
    {
        public Color DisplayColor;
        public Material MaterialOption;
    }

    [Serializable]
    public struct PartSlot
    {
        public SpriteLibraryAsset PartAsset;
        public CharacterPartSlotID SlotID;
    }

    public interface IValidate
    {
        public void Validate(CharacterCreatorData creatorData);
    }

    [Serializable]
    public class SpriteLibraryAssetSource : IValidate
    {
        public CategoryOptionsIndex CategoryOverride;
        public CharacterPartSlotID TargetSlotID;
        public SpriteLibraryAsset[] PartAssets = Array.Empty<SpriteLibraryAsset>();

        public SpriteLibraryAsset GetPartLibraryAsset(in DynamicBuffer<PersistentSkinOptionApply> persistentSkinOptionApplies)
        {
            int characterOptionIndex = CategoryOverride == CategoryOptionsIndex.None
                ? 0
                : persistentSkinOptionApplies[CategoryOverride.GetIndex()].Value;

            CollectionCustomHelper.CheckElementAccess(characterOptionIndex, PartAssets.Length);
            return PartAssets[characterOptionIndex];
        }

        public void Validate(CharacterCreatorData creatorData)
        {
            if ( CategoryOverride != CategoryOptionsIndex.None )
            {
                int optionSetsLength = creatorData[CategoryOverride].OptionSets.Length;
                if ( PartAssets.Length != optionSetsLength )
                {
                    PartAssets = PartAssets.CreateResizedCopy(optionSetsLength);
                    creatorData.SetDirtySafe();
                }
            }
            else if ( PartAssets.Length != 1 )
            {
                var firstAsset = PartAssets.FirstOrDefault();
                CollectionCustomHelper.Allocate(out PartAssets, 1);
                PartAssets[0] = firstAsset;
                creatorData.SetDirtySafe();
            }
        }
    }

    public enum CharacterPartSlotID
    {
        None = 0,
        Body = 1,
        Arms = 2,
        Tail = 3,
        Head = 4,
        Ears = 5,
        Eyes = 6,
        Nose = 7,
        Mouth = 8,
        Cheeks = 9,
        Extra = 10,
        Top = 11,
        Pattern = 12,
        Sleeves = 13,
        Pants = 14,
        Hat = 15,
        Accessory = 16,
        Accessory2 = 17,
        HandHeldItem = 18,
    }

    public static class CharacterCreationExtensions
    {
        public static readonly int CategoryCount = Enum.GetValues(typeof(CategoryOptionsIndex)).Length - 1;

        public static int GetIndex(this CharacterPartSlotID slotID)
        {
            return ((int)slotID) - 1;
        }

        public static int GetIndex(this CategoryOptionsIndex catId)
        {
            return ((int)catId) - 1;
        }
    }
}