using UnityEngine;
using UnityEngine.Serialization;

namespace GameProject
{
    /// <summary>
    /// This is a solution to loading bugs on a player at runtime when loading unityobjectrefs in a subscene,
    /// it also provide a solution in an hybrid (ecs/gameobject) scenario where the prefab is referencing itself but must target a prefab
    /// </summary>
    [CreateAssetMenu(menuName = GameProjectHelper.GAME_PROJECT_ASSET_MENU + "Create " + nameof(PrefabAssetReference), fileName = nameof(PrefabAssetReference))]
    public class PrefabAssetReference : ScriptableObject
    {
        [SerializeField] protected GameObject gameObjectRef;
        public GameObject gameObject => gameObjectRef;
    }
}