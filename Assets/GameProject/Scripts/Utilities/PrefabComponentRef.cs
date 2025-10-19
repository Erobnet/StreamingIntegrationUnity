#if UNITY_EDITOR
using UnityEditor;
#endif
using System.IO;
using UnityEngine;
using static ProjectFilePaths;

namespace GameProject
{
    /// <inheritdoc cref="PrefabAssetReference"/>
    public class PrefabComponentRef<T> : PrefabAssetReference
        where T : Component
    {
        [SerializeField] private T component;

        private void OnValidate()
        {
            if ( component && !gameObjectRef )
            {
                gameObjectRef = component.gameObject;
                this.SetDirtySafe();
            }
        }

        public T Component {
            get => component;
            set {
                gameObjectRef = value.gameObject;
                component = value;
            }
        }

        public static implicit operator T(PrefabComponentRef<T> prefabComponentRef) => prefabComponentRef.Component;
    }

    public static class PrefabComponentRefExtensions
    {
#if UNITY_EDITOR
        public static void ManagePrefabComponentRef<TComponent, TPrefabComponentRef>(ref TPrefabComponentRef prefabComponentRef, TComponent componentToReference, string assetPathOfReference)
            where TComponent : Component
            where TPrefabComponentRef : PrefabComponentRef<TComponent>
        {
            if ( BuildPipeline.isBuildingPlayer || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isUpdating )
                return;

            bool componentToReferenceExist = componentToReference;
            bool prefabComponentRefExist = prefabComponentRef;
            if ( componentToReferenceExist && !prefabComponentRefExist )
            {
                if ( AssetDatabase.AssetPathExists(assetPathOfReference) )
                {
                    prefabComponentRef = AssetDatabase.LoadAssetAtPath<TPrefabComponentRef>(assetPathOfReference);
                    if ( prefabComponentRef )
                    {
                        prefabComponentRef.Component = componentToReference;
                        prefabComponentRef.SetDirtySafe();
                    }
                }
                else
                {
                    componentToReference.CreatePrefabComponentRefAsset(assetPathOfReference, out prefabComponentRef);
                }
            }
            else if ( !componentToReferenceExist && prefabComponentRefExist )
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(prefabComponentRef));
                prefabComponentRef = null;
            }

            if ( prefabComponentRef && prefabComponentRef.Component != componentToReference )
            {
                prefabComponentRef.Component = componentToReference;
                prefabComponentRef.SetDirtySafe();
            }
        }

        public static void CreatePrefabComponentRefAsset<TPrefabComponentRef, TComponent>(this TComponent componentToRef, string constructPrefabRefAssetPath, out TPrefabComponentRef prefabComponentRef)
            where TPrefabComponentRef : PrefabComponentRef<TComponent>
            where TComponent : Component
        {
            prefabComponentRef = ScriptableObject.CreateInstance<TPrefabComponentRef>();
            prefabComponentRef.Component = componentToRef;
            EnsureFolderIsCreated(EDITORS_UNITYOBJECT_PROXY_FOLDER_PATH);
            AssetDatabase.CreateAsset(prefabComponentRef, AssetDatabase.GenerateUniqueAssetPath(constructPrefabRefAssetPath));
        }

        public static string ConstructPrefabRefAssetPath<TDataHolder>(string fullFileName)
        {
            return Path.Combine(EDITORS_UNITYOBJECT_PROXY_FOLDER_PATH, $"{typeof(TDataHolder).Name}_{fullFileName}.asset");
        }
#endif
    }
}