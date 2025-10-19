using UnityEngine;

namespace GameProject.Animation
{
    [CreateAssetMenu(menuName = GameProjectHelper.GAME_PROJECT_ASSET_MENU + "Create " + nameof(CharacterAnimationRootPrefabRef), fileName = nameof(CharacterAnimationRootPrefabRef))]
    public class CharacterAnimationRootPrefabRef : PrefabComponentRef<CharacterAnimationRoot>
    { }
}