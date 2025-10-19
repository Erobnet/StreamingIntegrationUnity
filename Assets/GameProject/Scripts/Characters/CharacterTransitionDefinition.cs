using System;
using UnityEngine;
using static GameProject.GameProjectHelper;

namespace GameProject.Characters
{
    [CreateAssetMenu(menuName = GAME_PROJECT_ASSET_MENU + nameof(CharacterTransitionDefinition), fileName = nameof(CharacterTransitionDefinition))]
    public class CharacterTransitionDefinition : ScriptableObject
    {
        public string DisplayName;
        public ColorApplyPartData[] ApplyColorData;
    }
}