using System;
using System.Diagnostics;
using GameProject.Animation;
using GameProject.Characters;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Properties;
using UnityEngine.UIElements;

namespace GameProject
{
    [Serializable]
    public struct ChangeTracker<T>
        where T : unmanaged, IEquatable<T>
    {
        [CreateProperty] private T _value;
        private bool _hasChanged;

        public ChangeTracker(in T value)
        {
            _value = value;
            _hasChanged = false;
        }

        public T Value {
            readonly get => _value;
            set {
                _hasChanged = HasChanged | !_value.Equals(value);
                _value = value;
            }
        }

        public bool HasChanged => _hasChanged;

        public bool UpdateHasChanged()
        {
            var hasChanged = HasChanged;
            _hasChanged = false;
            return hasChanged;
        }

        public static implicit operator T(ChangeTracker<T> tracker) => tracker.Value;
    }

    public class CharacterCustomizationUIField
    {
        private readonly Button _previous;
        private readonly Button _next;
        private readonly Label _displayTitle;
        private readonly Label _displayText;
        private readonly VisualElement _colorRoot;
        private readonly VisualTreeAsset _colorTemplate;
        private NativeReference<ChangeTracker<int>> _skinIndexReference;
        private NativeReference<ChangeTracker<int>> _colorIndexReference;

        public int SkinIndexSelector {
            get => _skinIndexReference.Value.Value;
            private set => _skinIndexReference.AsRef().Value = value;
        }

        public int ColorIndexReference {
            get => _colorIndexReference.Value.Value;
            private set => _colorIndexReference.AsRef().Value = value;
        }

        private readonly CharacterAnatomyCategory _transitionCategory;
        [CanBeNull] private CharacterAnimationRoot _characterAnimationRoot;

        [CanBeNull] public CharacterAnimationRoot CharacterAnimationRoot {
            get => _characterAnimationRoot;
            set {
#if UNITY_EDITOR
                //only for testing in the editor
                if ( !_skinIndexReference.IsCreated && _characterAnimationRoot )
                {
                    _skinIndexReference = new(Allocator.Domain);
                    _colorIndexReference = new(Allocator.Domain);
                }
#endif
                _characterAnimationRoot = value;
            }
        }

        public CharacterCustomizationUIField(VisualElement localRoot, CharacterAnatomyCategory transitionCategory,
            VisualTreeAsset colorTemplate)
        {
            _colorRoot = localRoot.Q("ColorList");
            _colorTemplate = colorTemplate;
            _next = localRoot.Q<Button>("Next");
            _previous = localRoot.Q<Button>("Previous");
            _displayTitle = localRoot.Q<Label>("Label");
            _displayText = localRoot.Q<Label>("Text");
            _transitionCategory = transitionCategory;
        }

        public void Initialize(NativeReference<ChangeTracker<int>> selectorSkinIndexReference, NativeReference<ChangeTracker<int>> selectorColorIndexReference)
        {
            _skinIndexReference = selectorSkinIndexReference;
            _colorIndexReference = selectorColorIndexReference;
            _previous.UnregisterCallback<ClickEvent>(OnPreviousOptionClick);
            _next.UnregisterCallback<ClickEvent>(OnNextOptionClick);
            _previous.RegisterCallback<ClickEvent>(OnPreviousOptionClick);
            _next.RegisterCallback<ClickEvent>(OnNextOptionClick);
            _colorRoot.Clear();
            var libraryTransitionData = _transitionCategory.OptionSets[SkinIndexSelector];
            DisplayColorOptions(libraryTransitionData);
            _displayTitle.text = _transitionCategory.CategoryDisplayName + ": ";
            _displayText.text = libraryTransitionData.DisplayName;
        }

        private void OnPreviousOptionClick(ClickEvent evt)
        {
            if ( SkinIndexSelector == 0 )
            {
                SkinIndexSelector = _transitionCategory.OptionSets.Length - 1;
            }
            else
            {
                SkinIndexSelector--;
            }

            ApplyCategoryByIndex(_transitionCategory.OptionSets, SkinIndexSelector);
        }

        private void OnNextOptionClick(ClickEvent evt)
        {
            if ( SkinIndexSelector == _transitionCategory.OptionSets.Length - 1 )
            {
                SkinIndexSelector = 0;
            }
            else
            {
                SkinIndexSelector++;
            }

            ApplyCategoryByIndex(_transitionCategory.OptionSets, SkinIndexSelector);
        }

        [Conditional("UNITY_EDITOR")]
        private void ApplyCharacterPart(LibraryTransitionData characterPartData)
        {
            if ( !CharacterAnimationRoot )
                return;

            foreach ( var part in characterPartData.SpriteLibraryAssets_Sets )
            {
                CharacterAnimationRoot.TrySetSpriteLibrarySlot(part);
            }

            CharacterAnimationRoot.ResyncAnimation();
        }

        private void ApplyCategoryByIndex(LibraryTransitionData[] optionForCategory, int index)
        {
            var libraryTransitionData = optionForCategory[index];
            _displayText.text = libraryTransitionData.DisplayName;
            DisplayColorOptions(libraryTransitionData);
            ApplyCharacterPart(libraryTransitionData);
        }


        private void DisplayColorOptions(LibraryTransitionData libraryTransitionData)
        {
            var colorOptions = libraryTransitionData.ColorOptions;
            var displayColorDifference = colorOptions.Length - _colorRoot.childCount;
            for ( int i = _colorRoot.childCount; i < colorOptions.Length; i++ )
            {
                _colorTemplate.CloneTree(_colorRoot);
            }

            if ( displayColorDifference < 0 )
            {
                for ( int i = colorOptions.Length; i < _colorRoot.childCount; i++ )
                {
                    _colorRoot[i].style.display = DisplayStyle.None;
                }
            }

            for ( int i = 0; i < colorOptions.Length; i++ )
            {
                var localIndex = i;
                var displayElement = _colorRoot[i];
                displayElement.style.display = DisplayStyle.Flex;
                var colorComponent = displayElement.Q("Color");
                colorComponent.style.unityBackgroundImageTintColor = colorOptions[i].DisplayColor;
                displayElement.UnregisterCallback<ClickEvent>(OnColorOptionClick);
                displayElement.RegisterCallback<ClickEvent>(OnColorOptionClick);

                void OnColorOptionClick(ClickEvent evt)
                {
                    ColorIndexReference = localIndex;
#if UNITY_EDITOR
                    if ( !CharacterAnimationRoot )
                        return;

                    for ( int partIndex = 0; partIndex < _transitionCategory.ApplyColorData.Length; partIndex++ )
                    {
                        CharacterAnimationRoot.SetSpriteRendererMaterial(_transitionCategory.ApplyColorData[partIndex].SlotID, colorOptions[localIndex].MaterialOption);
                    }
#endif
                }
            }
        }
    }
}