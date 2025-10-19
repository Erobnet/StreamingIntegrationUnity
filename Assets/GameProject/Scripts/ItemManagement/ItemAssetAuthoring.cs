using System;
using Drboum.Utilities.Runtime.EditorHybrid;
using GameProject.Characters;
using GameProject.Persistence.CommonData;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Localization;
using Material = UnityEngine.Material;

namespace GameProject.ItemManagement
{
    [CreateAssetMenu(menuName = GameProjectHelper.GAME_PROJECT_ASSET_MENU + "/" + nameof(ItemAssetAuthoring), fileName = nameof(ItemAssetAuthoring))]
    public class ItemAssetAuthoring : AssetReferenceID, IEquatable<ItemAssetAuthoring>
    {
        public LocalizedString DisplayName;
        public LocalizedString Description;
        public Sprite Icon;
        [Header("ShopProperties")]
        public GameCurrency PurchaseCost;
        [Header("Character Transition description")]
        public CharacterTransitionData CharacterTransitionData;
        public Material ColorOptions;

        public bool Equals(ItemAssetAuthoring other)
        {
            return base.Equals(other);
        }

        public static ItemAssetAuthoring Null => null;
    }

    public struct ItemAssetData
    {
        public GameCurrency PurchaseCost;
    }

    public struct ItemAssetDataReference : IComponentData
    {
        public BlobAssetReference<ItemAssetData> Value;
        public GameCurrency PurchaseCost => Value.Value.PurchaseCost;
    }
}