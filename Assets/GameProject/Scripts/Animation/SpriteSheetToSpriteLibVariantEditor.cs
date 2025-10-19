#if UNITY_EDITOR
using System;
using System.IO;
using GameProject.UI;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using static ProjectFilePaths;

namespace GameProject.Animation
{
    /// <summary>
    /// Editor tool to generate spritelibs asset taking texture2D spritesheets as inputs 
    /// </summary>
    public class SpriteSheetToSpriteLibVariantEditor : EditorWindow
    {
        private const string _BACK_UP_DATA_PATH = EDITORS_GENERATED_ASSET_FOLDER_PATH + "SpriteSheetToSpriteLibVariantData.asset";
        private SpriteSheetToSpriteLibVariantData _creatorSpriteVariantData;
        private DragAndDropProjectObjectsManipulator<Texture2D> _editorDragAndDrop;

        public void CreateGUI()
        {
            Directory.CreateDirectory(EDITORS_GENERATED_ASSET_FOLDER_PATH);
            if ( File.Exists(_BACK_UP_DATA_PATH) )
            {
                _creatorSpriteVariantData = AssetDatabase.LoadAssetAtPath<SpriteSheetToSpriteLibVariantData>(_BACK_UP_DATA_PATH);
            }
            if ( _creatorSpriteVariantData == null )
            {
                if ( AssetDatabase.AssetPathExists(_BACK_UP_DATA_PATH) )
                {
                    AssetDatabase.DeleteAsset(_BACK_UP_DATA_PATH); //the backup file is corrupt so we clean it up and create a new one
                }
                _creatorSpriteVariantData = CreateInstance<SpriteSheetToSpriteLibVariantData>();
                AssetDatabase.CreateAsset(_creatorSpriteVariantData, _BACK_UP_DATA_PATH);
                AssetDatabase.SaveAssets();
            }
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;

            var folderToExport = CreateObjectPicker(_creatorSpriteVariantData.DestinationFolderObject, ObjectNames.NicifyVariableName(nameof(_creatorSpriteVariantData.DestinationFolderObject)), (newVal) =>
            {
                _creatorSpriteVariantData.DestinationFolderObject = newVal;
                SetDirtyBackEndData();
            });
            root.Add(folderToExport);

            // VisualElements objects can contain other VisualElement following a tree hierarchy
            var mainSpriteLibraryAsset = CreateObjectPicker(_creatorSpriteVariantData.ParentLibrary, ObjectNames.NicifyVariableName(nameof(_creatorSpriteVariantData.ParentLibrary)), (newVal) =>
            {
                _creatorSpriteVariantData.ParentLibrary = newVal;
                SetDirtyBackEndData();
            });
            root.Add(mainSpriteLibraryAsset);

            var listview = CreateListView(root);

            // Create button
            var button = new Button {
                name = "Create Sprite Library Variant",
                text = "Create Sprite Library Variant"
            };
            button.clicked += () =>
            {
                SpriteSheetToSpriteLibVariantData.CreateVariant(_creatorSpriteVariantData);
                listview.Rebuild();
            };
            root.Add(button);
        }

        private ListView CreateListView(VisualElement root)
        {
            // Provide the list view with an explict height for every row
            // so it can calculate how many items to actually display
            const int itemHeight = 16;

            var listView = new ListView(_creatorSpriteVariantData.NewSpriteSheets, itemHeight, MakeItem, BindItem) {
                selectionType = SelectionType.Single,
                focusable = true,
                showAddRemoveFooter = true,
                showFoldoutHeader = true,
                headerTitle = "Sprite Textures sources",
                tooltip = "Texture to generate sprite library variant from",
            };
            listView.AddToClassList("drop-area");
            _editorDragAndDrop = new(listView);

            listView.RegisterCallback<DragPerformEvent>(evt =>
            {
                foreach ( var droppedObject in _editorDragAndDrop.DroppedObjects )
                {
                    listView.itemsSource.Add(droppedObject);
                }
                listView.RefreshItems();
            });
            root.Add(listView);
            return listView;

            // The "makeItem" function is called when the
            // ListView needs more items to render.
            VisualElement MakeItem() => new ObjectField { objectType = typeof(Texture2D) };

            // As the user scrolls through the list, the ListView object
            // recycles elements created by the "makeItem" function,
            // and invoke the "bindItem" callback to associate
            // the element with the matching data item (specified as an index in the list).
            void BindItem(VisualElement e, int i)
            {
                var objectField = (e as ObjectField);
                objectField.RegisterValueChangedCallback((changeEvent) =>
                {
                    _creatorSpriteVariantData.NewSpriteSheets[i] = (Texture2D)changeEvent.newValue;
                    SetDirtyBackEndData();
                });
                objectField.SetValueWithoutNotify(_creatorSpriteVariantData.NewSpriteSheets[i]);
            }
        }

        private void SetDirtyBackEndData()
        {
            EditorUtility.SetDirty(_creatorSpriteVariantData);
            AssetDatabase.SaveAssets();
        }

        private ObjectField CreateObjectPicker<TObject>(TObject obj, string objectLabel, Action<TObject> callback)
            where TObject : Object
        {
            var objectPicker = new ObjectField(objectLabel) {
                objectType = typeof(TObject),
                value = obj
            };
            objectPicker.RegisterValueChangedCallback((evt =>
            {
                callback.Invoke((TObject)evt.newValue);
            }));
            return objectPicker;
        }


        [MenuItem("Tools/" + nameof(SpriteSheetToSpriteLibVariantEditor))]
        public static void ShowGeneratorWindow()
        {
            var wnd = GetWindow<SpriteSheetToSpriteLibVariantEditor>();
            wnd.titleContent = new GUIContent(nameof(SpriteSheetToSpriteLibVariantEditor));
        }
    }
}
#endif