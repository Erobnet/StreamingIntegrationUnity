using System;
using System.IO;
using Drboum.Utilities.Entities;
using Drboum.Utilities.Runtime.EditorHybrid;
using GameProject.Persistence.CommonData;
using Unity.Entities;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace GameProject.WorldObjectPlacement
{

    [CreateAssetMenu(menuName = GameProjectHelper.GAME_PROJECT_ASSET_MENU + nameof(PlaceableObjectIndexer), fileName = nameof(PlaceableObjectIndexer))]
    public class PlaceableObjectIndexer : AssetReferenceID
    {
        [SerializeField] private PlaceableObjectAuthoring[] placeableObjectOptions = Array.Empty<PlaceableObjectAuthoring>();
        [SerializeField] private Sprite displayIcon;
        [SerializeField] private int availableStock = -1;
        [SerializeField] private GameCurrency purchasePrice = new() { Value = 1 };
        [SerializeField] private bool canBeDeleted = true;

        public GameCurrency PurchasePrice => purchasePrice;
        public bool CanBeDeleted => canBeDeleted;
        public int AvailableStock => availableStock;
        public Sprite DisplayIcon => displayIcon;
        public PlaceableObjectAuthoring[] PlaceableObjectOptions => placeableObjectOptions;

        protected override void OnValidate()
        {
            base.OnValidate();
            SetDefaultIcon();
            UpdatePurchasePrice();
        }

        private void UpdatePurchasePrice()
        {
            foreach ( var placeableObjectAuthoring in placeableObjectOptions )
            {
                if ( placeableObjectAuthoring && placeableObjectAuthoring.CanBeDeleted != canBeDeleted )
                {
                    placeableObjectAuthoring.CanBeDeleted = canBeDeleted;
                    placeableObjectAuthoring.SetDirtySafe();
                }
            }
        }

        public void SetDefaultIcon()
        {
            if ( !displayIcon && placeableObjectOptions.Length > 0 && placeableObjectOptions[0].Renderers.Count > 0 )
            {
                displayIcon = placeableObjectOptions[0].Renderers[0].sprite;
                this.SetDirtySafe();
            }
        }

#if UNITY_EDITOR
        [MenuItem(GameProjectHelper.GAME_PROJECT_ASSET_MENU + nameof(PlaceableObjectIndexer) + "/" + nameof(CreatePlaceableObjectIndexerFromSelection))]
        public static void CreatePlaceableObjectIndexerFromSelection()
        {
            foreach ( var o in Selection.objects )
            {
                if ( o is GameObject go
                     && go.IsPrefabAssetRoot()
                     && go.TryGetComponent(out PlaceableObjectAuthoring placeableObjectAuthoring) )
                {
                    string instanceName = $"{placeableObjectAuthoring.name}_Indexer";
                    string assetPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(go)) + $"\\{instanceName}.asset";
                    if ( !AssetDatabase.AssetPathExists(assetPath) )
                    {
                        var instance = CreateInstance<PlaceableObjectIndexer>();
                        instance.name = instanceName;
                        instance.placeableObjectOptions = new[] { placeableObjectAuthoring };
                        instance.SetDefaultIcon();
                        AssetDatabase.CreateAsset(instance, assetPath);
                    }
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
#endif
    }

    public struct PlaceableObjectIndex : IBufferElementData
    {
        public EntityWith<PlaceableObjectData> Prefab;
    }
}