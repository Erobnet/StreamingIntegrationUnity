using System;
using UnityEngine;
using static GameProject.GameProjectHelper;

namespace GameProject.Characters
{
    [CreateAssetMenu(menuName = GAME_PROJECT_ASSET_MENU + nameof(CharacterTransitionData), fileName = nameof(CharacterTransitionData))]
    [Serializable]
    public class CharacterTransitionData : ScriptableObject
    {
#if UNITY_EDITOR
        [SerializeField] private CharacterCreatorData ProjectCharacterCreatorData;
#endif
        public string DisplayName;
        public SpriteLibraryAssetSource[] SpriteLibraryAssetSets = Array.Empty<SpriteLibraryAssetSource>();
        public CharacterTransitionDefinition CharacterTransitionDefinition;

#if UNITY_EDITOR
        private void OnValidate()
        {
            Validate(ProjectCharacterCreatorData);
        }
#endif

        public void Validate(CharacterCreatorData characterCreatorData)
        {
            for ( var index = 0; index < SpriteLibraryAssetSets.Length; index++ )
            {
                var libraryAssetSource = SpriteLibraryAssetSets[index];
                libraryAssetSource.Validate(characterCreatorData);
            }
        }
    }
}