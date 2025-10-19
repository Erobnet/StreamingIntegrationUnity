using System.Diagnostics;
using System.IO;
using ProjectDawn.Navigation;
using Unity.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace GameProject
{
    public static class GameProjectHelper
    {
        public const string GAME_PROJECT_ASSET_MENU = nameof(GameProject) + "/";

        public static string NicifyVariable(this string variableName)
        {
            return variableName.Replace("_", "");
        }

        public static unsafe void AddRange<TNativeList>(this ref TNativeList serializableComponentList, void* ptrSource, int byteLength)
            where TNativeList : unmanaged, IUTF8Bytes, INativeList<byte>
        {
            serializableComponentList.AddRangeExt<TNativeList, byte>(ptrSource, byteLength);
        }

        public static bool HasArrived(this in AgentBody agentBody, float remainingDistance = 1f)
        {
            return agentBody.IsStopped && agentBody.RemainingDistance < remainingDistance;
        }


        [Conditional("UNITY_EDITOR")]
        public static void AssertIsPartOfPrefab(Object authoring, GameObject gameObject, string authoringPropertyPrefabName)
        {
#if UNITY_EDITOR
            if ( !PrefabUtility.IsPartOfPrefabAsset(gameObject) )
            {
                string message = $"the gameobject named '{gameObject.name}' passed to the property '{authoringPropertyPrefabName}' must be a prefab asset";
                Debug.LogError(message, authoring);
                throw new InvalidDataException(message);
            }
#endif
        }

        public static void SetElementLayoutEnabled(this VisualElement visualElement, bool enabled)
        {
            visualElement.style.display = enabled ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}