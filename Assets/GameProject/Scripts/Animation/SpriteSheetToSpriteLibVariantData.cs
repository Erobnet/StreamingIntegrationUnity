#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Animation;
using UnityEngine;
using UnityEngine.U2D.Animation;
using Object = UnityEngine.Object;

namespace GameProject.Animation
{
    [Serializable]
    public class SpriteSheetToSpriteLibVariantData : ScriptableObject
    {
        [Header("Main Sprite Library to Create Variant From")]
        public SpriteLibraryAsset ParentLibrary;

        [Header("New Sliced Sprite Sheet")]
        public List<Texture2D> NewSpriteSheets;
        [Tooltip("Root Folder where the variant will be created.")]
        public Object DestinationFolderObject;

        public void OnValidate()
        {
            if ( DestinationFolderObject && !AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(DestinationFolderObject)) )
            {
                Debug.LogError($"expected folder type but was instead {DestinationFolderObject.GetType()}");
                DestinationFolderObject = null;
                EditorUtility.SetDirty(this);
            }
        }

        [ContextMenu("Create Sprite Library Variant")]
        public static void CreateVariant(SpriteSheetToSpriteLibVariantData converterData)
        {
            var newSpriteSheets = converterData.NewSpriteSheets;
            var mainLibrary = converterData.ParentLibrary;
            var folderObject = converterData.DestinationFolderObject;
            if ( mainLibrary == null || newSpriteSheets == null )
            {
                Debug.LogError("Main Library or new sprite sheet is missing!");
                return;
            }

            string targetFolderPath = AssetDatabase.GetAssetPath(folderObject);

            // Create a new instance of the SpriteLibraryAsset
            for ( var spriteSheetIndex = newSpriteSheets.Count - 1; spriteSheetIndex >= 0; spriteSheetIndex-- )
            {
                Texture2D newSpriteSheet = newSpriteSheets[spriteSheetIndex];
                var newVariant = Instantiate(mainLibrary);
                string newVariantPath = $"{targetFolderPath}/{newSpriteSheet.name}";

                //grab name from spritesheet
                newVariant.name = newSpriteSheet.name;
                var assetPath = AssetDatabase.GetAssetPath(newSpriteSheet);
                var allAssetsAtPath = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                int i = 0;
                // Iterate over all categories and entries to replace labels
                foreach ( var category in mainLibrary.GetCategoryNames() )
                {
                    var categoryLabelNamesEnumerable = mainLibrary.GetCategoryLabelNames(category);
                    foreach ( var label in categoryLabelNamesEnumerable )
                    {
                        var asset = allAssetsAtPath[i];
                        if ( AssetDatabase.IsMainAsset(asset) )
                            continue;

                        var newSprite = (Sprite)asset;
                        // Update the sprite for the variant
                        newVariant.AddCategoryLabel(newSprite, category, label);
                        i++;
                    }
                }

                newVariant.SaveAsSourceAsset(newVariantPath, AssetDatabase.GetAssetPath(mainLibrary));
                Debug.Log("Sprite Library Variant created at: " + newVariantPath, newVariant);
                newSpriteSheets.RemoveAt(spriteSheetIndex);
                EditorUtility.SetDirty(converterData);
            }

            // Save the new variant as an asset
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
#endif