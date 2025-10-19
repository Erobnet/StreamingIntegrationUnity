using System;
using System.Collections;
using System.Collections.Generic;
using GameProject.Animation;
using GameProject.Characters;
using GameProject.Inputs;
using GameProject.WorldObjectPlacement;
using Unity.Assertions;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

namespace GameProject
{
    public readonly struct VisualElementWrapper :
        ITransitionAnimations,
        IExperimentalFeatures
    {
        public VisualElement Value { get; }

        public VisualElementWrapper(VisualElement value)
        {
            Value = value;
        }

        public bool IsOpen {
            get => Value.style.display == DisplayStyle.Flex;
            set => Value.SetElementLayoutEnabled(value);
        }

        public IStyle style => Value.style;

        public VisualElement Q(string name = null, string className = null)
        {
            return Value.Q(name, className);
        }

        public T Q<T>(string name = null, string className = null)
            where T : VisualElement
        {
            return Value.Q<T>(name, className);
        }

        public void RegisterCallback<TEventType>(
            EventCallback<TEventType> callback,
            TrickleDown useTrickleDown = TrickleDown.NoTrickleDown)
            where TEventType : EventBase<TEventType>, new()
        {
            Value.RegisterCallback<TEventType>(callback, useTrickleDown);
        }

        public void RegisterCallbackOnce<TEventType>(
            EventCallback<TEventType> callback,
            TrickleDown useTrickleDown = TrickleDown.NoTrickleDown)
            where TEventType : EventBase<TEventType>, new()
        {
            Value.RegisterCallbackOnce<TEventType>(callback, useTrickleDown);
        }

        public void RegisterCallback<TEventType, TUserArgsType>(
            EventCallback<TEventType, TUserArgsType> callback,
            TUserArgsType userArgs,
            TrickleDown useTrickleDown = TrickleDown.NoTrickleDown)
            where TEventType : EventBase<TEventType>, new()
        {
            Value.RegisterCallback<TEventType, TUserArgsType>(callback, userArgs, useTrickleDown);
        }

        public void RegisterCallbackOnce<TEventType, TUserArgsType>(
            EventCallback<TEventType, TUserArgsType> callback,
            TUserArgsType userArgs,
            TrickleDown useTrickleDown = TrickleDown.NoTrickleDown)
            where TEventType : EventBase<TEventType>, new()
        {
            Value.RegisterCallbackOnce<TEventType, TUserArgsType>(callback, userArgs, useTrickleDown);
        }

        public void UnregisterCallback<TEventType>(
            EventCallback<TEventType> callback,
            TrickleDown useTrickleDown = TrickleDown.NoTrickleDown)
            where TEventType : EventBase<TEventType>, new()
        {
            Value.UnregisterCallback<TEventType>(callback, useTrickleDown);
        }

        public void UnregisterCallback<TEventType, TUserArgsType>(
            EventCallback<TEventType, TUserArgsType> callback,
            TrickleDown useTrickleDown = TrickleDown.NoTrickleDown)
            where TEventType : EventBase<TEventType>, new()
        {
            Value.UnregisterCallback<TEventType, TUserArgsType>(callback, useTrickleDown);
        }

        #region VisualElementGeneratedMethodWrappers
        public ValueAnimation<float> Start(float from, float to, int durationMs, Action<VisualElement, float> onValueChanged)
        {
            return ((ITransitionAnimations)Value).Start(from, to, durationMs, onValueChanged);
        }

        public ValueAnimation<Rect> Start(Rect from, Rect to, int durationMs, Action<VisualElement, Rect> onValueChanged)
        {
            return ((ITransitionAnimations)Value).Start(from, to, durationMs, onValueChanged);
        }

        public ValueAnimation<Color> Start(Color from, Color to, int durationMs, Action<VisualElement, Color> onValueChanged)
        {
            return ((ITransitionAnimations)Value).Start(from, to, durationMs, onValueChanged);
        }

        public ValueAnimation<Vector3> Start(Vector3 from, Vector3 to, int durationMs, Action<VisualElement, Vector3> onValueChanged)
        {
            return ((ITransitionAnimations)Value).Start(from, to, durationMs, onValueChanged);
        }

        public ValueAnimation<Vector2> Start(Vector2 from, Vector2 to, int durationMs, Action<VisualElement, Vector2> onValueChanged)
        {
            return ((ITransitionAnimations)Value).Start(from, to, durationMs, onValueChanged);
        }

        public ValueAnimation<Quaternion> Start(Quaternion from, Quaternion to, int durationMs, Action<VisualElement, Quaternion> onValueChanged)
        {
            return ((ITransitionAnimations)Value).Start(from, to, durationMs, onValueChanged);
        }

        public ValueAnimation<StyleValues> Start(StyleValues from, StyleValues to, int durationMs)
        {
            return ((ITransitionAnimations)Value).Start(from, to, durationMs);
        }

        public ValueAnimation<StyleValues> Start(StyleValues to, int durationMs)
        {
            return ((ITransitionAnimations)Value).Start(to, durationMs);
        }

        public ValueAnimation<float> Start(Func<VisualElement, float> fromValueGetter, float to, int durationMs, Action<VisualElement, float> onValueChanged)
        {
            return ((ITransitionAnimations)Value).Start(fromValueGetter, to, durationMs, onValueChanged);
        }

        public ValueAnimation<Rect> Start(Func<VisualElement, Rect> fromValueGetter, Rect to, int durationMs, Action<VisualElement, Rect> onValueChanged)
        {
            return ((ITransitionAnimations)Value).Start(fromValueGetter, to, durationMs, onValueChanged);
        }

        public ValueAnimation<Color> Start(Func<VisualElement, Color> fromValueGetter, Color to, int durationMs, Action<VisualElement, Color> onValueChanged)
        {
            return ((ITransitionAnimations)Value).Start(fromValueGetter, to, durationMs, onValueChanged);
        }

        public ValueAnimation<Vector3> Start(Func<VisualElement, Vector3> fromValueGetter, Vector3 to, int durationMs, Action<VisualElement, Vector3> onValueChanged)
        {
            return ((ITransitionAnimations)Value).Start(fromValueGetter, to, durationMs, onValueChanged);
        }

        public ValueAnimation<Vector2> Start(Func<VisualElement, Vector2> fromValueGetter, Vector2 to, int durationMs, Action<VisualElement, Vector2> onValueChanged)
        {
            return ((ITransitionAnimations)Value).Start(fromValueGetter, to, durationMs, onValueChanged);
        }

        public ValueAnimation<Quaternion> Start(Func<VisualElement, Quaternion> fromValueGetter, Quaternion to, int durationMs, Action<VisualElement, Quaternion> onValueChanged)
        {
            return ((ITransitionAnimations)Value).Start(fromValueGetter, to, durationMs, onValueChanged);
        }

        public ValueAnimation<Rect> Layout(Rect to, int durationMs)
        {
            return ((ITransitionAnimations)Value).Layout(to, durationMs);
        }

        public ValueAnimation<Vector2> TopLeft(Vector2 to, int durationMs)
        {
            return ((ITransitionAnimations)Value).TopLeft(to, durationMs);
        }

        public ValueAnimation<Vector2> Size(Vector2 to, int durationMs)
        {
            return ((ITransitionAnimations)Value).Size(to, durationMs);
        }

        public ValueAnimation<float> Scale(float to, int duration)
        {
            return ((ITransitionAnimations)Value).Scale(to, duration);
        }

        public ValueAnimation<Vector3> Position(Vector3 to, int duration)
        {
            return ((ITransitionAnimations)Value).Position(to, duration);
        }

        public ValueAnimation<Quaternion> Rotation(Quaternion to, int duration)
        {
            return ((ITransitionAnimations)Value).Rotation(to, duration);
        }

        public ITransitionAnimations animation => ((IExperimentalFeatures)Value).animation;
        #endregion

        public static implicit operator VisualElementWrapper(VisualElement wrapper)
        {
            return new VisualElementWrapper(wrapper);
        }

        public static implicit operator VisualElement(VisualElementWrapper wrapper)
        {
            return wrapper.Value;
        }
    }

    public class GlobalUIController : MonoBehaviour, IDocumentUIProvider
    {
        private static readonly int _AnimalTypeCategoryIndex = CategoryOptionsIndex.Base.GetIndex();
        private static readonly int _EyesTypeCatIndex = CategoryOptionsIndex.Eyes.GetIndex();
        private static readonly int _NoseTypeCatIndex = CategoryOptionsIndex.Nose.GetIndex();
        private static readonly int _CheeksTypeCatIndex = CategoryOptionsIndex.Cheek.GetIndex();
        private static readonly int _TopsTypeCatIndex = CategoryOptionsIndex.Top.GetIndex();
        public event Action<int> OnSelectedBuildObject = delegate { };
        public event Action<bool> OnToggleBuildMenu = delegate { };

        [SerializeField] private Camera mirrorCamera;
        [SerializeField] private Sprite ButtonDefaultSprite;
        [SerializeField] private Sprite ButtonPressedSprite;
        [SerializeField] private Sprite PlayerActionDefaultSprite;
        [SerializeField] private Sprite PlayerActionHoveredSprite;
        [SerializeField] private Sprite SettingsDefaultSprite;
        [SerializeField] private Sprite SettingsHoveredSprite;
        [SerializeField] private Sprite CharacterCreatorDefaultSprite;
        [SerializeField] private Sprite CharacterCreatorHoveredSprite;
        [SerializeField] private Sprite ButtonAvatarSelectedSprite;
        [SerializeField] private Sprite ButtonAvatarUnselectedSprite;
        [SerializeField] private Sprite ButtonOutfitSelectedSprite;
        [SerializeField] private Sprite ButtonOutfitUnselectedSprite;
        [SerializeField] private Sprite ButtonExitDefaultSprite;
        [SerializeField] private Sprite ButtonExitPressedSprite;
        [SerializeField] private CharacterCreatorData _characterCreatorData;
        [SerializeField] private VisualTreeAsset ColorTemplate;
#if UNITY_EDITOR
        [SerializeField, Tooltip("optional: for testing only in the editor")] private CharacterAnimationRoot characterAnimator;
#endif

        private readonly List<VisualElement> _priorityTargetsForEvents = new();
        private readonly CharacterCustomizationUIField[] _allCustomizationUIFields = new CharacterCustomizationUIField[CharacterCreationExtensions.CategoryCount];
        private readonly List<BuildObjectViewData> _placeableObjectPrefabs = new();
        private VisualElementWrapper _buildMenuListViewContentRoot;
        private VisualElementWrapper _dockPlayerActions;
        private VisualElementWrapper _expandedPlayerActionsRoot;
        private VisualElementWrapper _buttonPlayerActions;
        private VisualElementWrapper _iconPlayerActions;
        private VisualElementWrapper _buttonSettings;
        private VisualElementWrapper _iconSettings;
        private VisualElementWrapper _settingsRoot;
        private VisualElementWrapper _buttonCharacterCreator;
        private VisualElementWrapper _iconCharacterCreator;
        private VisualElementWrapper _characterCreatorRoot;
        private VisualElementWrapper _buttonAvatarSelection;
        private VisualElementWrapper _iconAvatarSelection;
        private VisualElementWrapper _bodySelections;
        private VisualElementWrapper _faceSelections;
        private VisualElementWrapper _buttonOutfitSelection;
        private VisualElementWrapper _iconOutfitSelection;
        private VisualElementWrapper _outfitSelections;
        private VisualElementWrapper _buttonExit;
        private VisualElementWrapper _iconExit;
        private VisualElementWrapper _buildMenuRoot;
        private ListView _buildMenuListView;
        private GameInputsAsset _gameInputsAsset;

        public Camera MirrorCamera => mirrorCamera;

        private CharacterCustomizationUIField _animalTypeUIRoot {
            get => _allCustomizationUIFields[_AnimalTypeCategoryIndex];
            set => _allCustomizationUIFields[_AnimalTypeCategoryIndex] = value;
        }

        private CharacterCustomizationUIField _eyesTypeUIRoot {
            get => _allCustomizationUIFields[_EyesTypeCatIndex];
            set => _allCustomizationUIFields[_EyesTypeCatIndex] = value;
        }

        private CharacterCustomizationUIField _noseTypeUIRoot {
            get => _allCustomizationUIFields[_NoseTypeCatIndex];
            set => _allCustomizationUIFields[_NoseTypeCatIndex] = value;
        }

        private CharacterCustomizationUIField _cheeksTypeUIRoot {
            get => _allCustomizationUIFields[_CheeksTypeCatIndex];
            set => _allCustomizationUIFields[_CheeksTypeCatIndex] = value;
        }

        private CharacterCustomizationUIField _topsTypeUIRoot {
            get => _allCustomizationUIFields[_TopsTypeCatIndex];
            set => _allCustomizationUIFields[_TopsTypeCatIndex] = value;
        }

        public UIDocument Document {
            get;
            private set;
        }

        public bool IsCharacterCreatorOpen {
            get => _characterCreatorRoot.IsOpen;
            set {
                _characterCreatorRoot.IsOpen = value;
                mirrorCamera.enabled = value;
            }
        }

        void Awake()
        {
            mirrorCamera.enabled = false;
            Document = GetComponent<UIDocument>();
            var root = Document.rootVisualElement;
            _dockPlayerActions = root.Q("Dock_PlayerActions");
            _expandedPlayerActionsRoot = root.Q("Expanded_PlayerActions");

            _buildMenuRoot = root.Q("BuildMenuRoot");
            _buildMenuListView = _buildMenuRoot.Q<ListView>();
            _buildMenuListView.bindItem = BindBuildObjectListItem;
            _buildMenuListView.selectionType = SelectionType.Single;
            _buildMenuListViewContentRoot = _buildMenuListView.Q("unity-content-container");
            SetBuildMenuSource(_placeableObjectPrefabs);

            _buttonPlayerActions = root.Q("Button_PlayerActions");
            _iconPlayerActions = root.Q("Icon_PlayerActions");

            _buttonSettings = root.Q("Button_Settings");
            _iconSettings = root.Q("Icon_Settings");
            _settingsRoot = root.Q("Settings");

            _buttonCharacterCreator = root.Q("Button_CharacterCreator");
            _iconCharacterCreator = root.Q("Icon_CharacterCreator");
            _characterCreatorRoot = root.Q("CharacterCreator");

            _bodySelections = _characterCreatorRoot.Q("BodySelections");
            _faceSelections = _characterCreatorRoot.Q("FaceSelections");
            _outfitSelections = _characterCreatorRoot.Q("OutfitSelections");

            _buttonAvatarSelection = root.Q("Icon_Avatar");
            _iconAvatarSelection = root.Q("Icon_Avatar");
            _buttonOutfitSelection = root.Q("Icon_Outfit");
            _iconOutfitSelection = root.Q("Icon_Outfit");
            _buttonExit = root.Q("ExitCharacterCreator");
            _iconExit = root.Q("Icon_Exit");

            _animalTypeUIRoot = new CharacterCustomizationUIField(root.Q("Field_Animal"), _characterCreatorData[_AnimalTypeCategoryIndex], ColorTemplate);
            _eyesTypeUIRoot = new CharacterCustomizationUIField(root.Q("Field_Eyes"), _characterCreatorData[_EyesTypeCatIndex], ColorTemplate);
            _noseTypeUIRoot = new CharacterCustomizationUIField(root.Q("Field_Nose"), _characterCreatorData[_NoseTypeCatIndex], ColorTemplate);
            _cheeksTypeUIRoot = new CharacterCustomizationUIField(root.Q("Field_Cheeks"), _characterCreatorData[_CheeksTypeCatIndex], ColorTemplate);
            _topsTypeUIRoot = new CharacterCustomizationUIField(root.Q("Field_Tops"), _characterCreatorData[_TopsTypeCatIndex], ColorTemplate);

#if UNITY_EDITOR
            foreach ( var customizationUIComponent in _allCustomizationUIFields )
            {
                customizationUIComponent.CharacterAnimationRoot = characterAnimator;
            }
#endif
            _expandedPlayerActionsRoot.IsOpen = false;
            _buildMenuRoot.IsOpen = false;
            IsCharacterCreatorOpen = false;
            _settingsRoot.IsOpen = false;
            _outfitSelections.IsOpen = false;

            _priorityTargetsForEvents.Add(_buttonExit);
            _priorityTargetsForEvents.Add(_buttonSettings);
            _priorityTargetsForEvents.Add(_buttonAvatarSelection);
            _priorityTargetsForEvents.Add(_buttonCharacterCreator);
            _priorityTargetsForEvents.Add(_buttonPlayerActions);
            _priorityTargetsForEvents.Add(_buildMenuRoot);
            _priorityTargetsForEvents.Add(_buildMenuListView);
            _priorityTargetsForEvents.Add(_dockPlayerActions);
            _priorityTargetsForEvents.Add(_expandedPlayerActionsRoot);
            _priorityTargetsForEvents.Add(_characterCreatorRoot.Q("Content"));
        }

        private void BindBuildObjectListItem(VisualElement rootElement, int i)
        {
            bool wasEnabled = rootElement.enabledSelf;
            SetImageBackground(rootElement.Q("ImageContent"), _placeableObjectPrefabs[i].BuildObjectData.DisplayIcon);
            var stockCountView = rootElement.Q<TextElement>("StockCount");
            bool trackedQuantityHasValue = _placeableObjectPrefabs[i].TrackedQuantity.HasValue;
            if ( trackedQuantityHasValue )
            {
                TrackedQuantity trackedQuantity = _placeableObjectPrefabs[i].TrackedQuantity.Value;
                stockCountView.text = trackedQuantity.Available.ToString();
            }
            rootElement.SetEnabled(_placeableObjectPrefabs[i].IsAvailable);

            stockCountView.style.display = trackedQuantityHasValue
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            if ( wasEnabled != rootElement.enabledSelf )
                _buildMenuListView.RefreshItem(i);
        }

        public void SetInputProvider(GameInputsAsset inputAsset)
        {
            _gameInputsAsset = inputAsset;
            _gameInputsAsset.UI.Build.performed -= OnOpenBuildMenuWrapper; // avoid duplicate
            _gameInputsAsset.UI.Build.performed += OnOpenBuildMenuWrapper;
        }

        public void SetBuildMenuItemSource(ReadOnlySpan<BuildObjectViewData> buildObjectPrefabs)
        {
            _placeableObjectPrefabs.Clear();
            for ( var index = 0; index < buildObjectPrefabs.Length; index++ )
            {
                var buildObjectPrefab = buildObjectPrefabs[index];
                _placeableObjectPrefabs.Add(buildObjectPrefab);
            }
            SetBuildMenuSource(_placeableObjectPrefabs);
        }

        private void SetBuildMenuSource(IList placeableObjectAuthorings)
        {
            _buildMenuListView.itemsSource = placeableObjectAuthorings;
            _buildMenuListView.RefreshItems();
        }

        private void OnEnable()
        {
            _buttonPlayerActions.RegisterCallback<MouseEnterEvent>(OnPlayerActionHover);
            _buttonPlayerActions.RegisterCallback<MouseLeaveEvent>(OnPlayerActionHoverExit);
            _buttonPlayerActions.RegisterCallback<MouseDownEvent>(OnPlayerActionClick);
            _buttonPlayerActions.RegisterCallback<MouseUpEvent>(OnPlayerActionClickReleased);

            _buttonSettings.RegisterCallback<MouseEnterEvent>(OnSettingsHover);
            _buttonSettings.RegisterCallback<MouseLeaveEvent>(OnSettingsHoverExit);
            _buttonSettings.RegisterCallback<MouseDownEvent>(OnSettingsClick);
            _buttonSettings.RegisterCallback<MouseUpEvent>(OnSettingsReleased);

            _buttonCharacterCreator.RegisterCallback<MouseEnterEvent>(OnCharacterCreatorHover);
            _buttonCharacterCreator.RegisterCallback<MouseLeaveEvent>(OnCharacterCreatorHoverExit);
            _buttonCharacterCreator.RegisterCallback<MouseDownEvent>(OnCharacterCreatorClick);
            _buttonCharacterCreator.RegisterCallback<MouseUpEvent>(OnCharacterCreatorReleased);

            _buttonOutfitSelection.RegisterCallback<MouseDownEvent>(OnOutfitSelectionClick);
            _buttonOutfitSelection.RegisterCallback<MouseUpEvent>(OnOutfitSelectionReleased);

            _buttonAvatarSelection.RegisterCallback<MouseDownEvent>(OnAvatarSelectionClick);
            _buttonAvatarSelection.RegisterCallback<MouseUpEvent>(OnAvatarSelectionReleased);

            _buttonExit.RegisterCallback<PointerDownEvent>(OnExitClick, TrickleDown.TrickleDown);
            _buttonExit.RegisterCallback<PointerUpEvent>(OnExitReleased, TrickleDown.TrickleDown);
            _buildMenuListViewContentRoot.RegisterCallback<ClickEvent>(OnSelectedBuildObjectWrapper);
            if ( _gameInputsAsset != null )
                _gameInputsAsset.UI.Build.performed += OnOpenBuildMenuWrapper;
        }

        private void OnSelectedBuildObjectWrapper(ClickEvent evt)
        {
            OnSelectedBuildObject(_buildMenuListView.selectedIndex);
        }

        private void OnOpenBuildMenuWrapper(InputAction.CallbackContext obj)
        {
            OnOpenBuildMenu();
        }

        private void OnOpenBuildMenu()
        {
            if ( _buildMenuRoot.IsOpen )
            {
                CloseBuildMenu();
            }
            else
            {
                OpenBuildMenu();
            }
        }

        private void CloseBuildMenu()
        {
            _buildMenuRoot.IsOpen = false;
            OnToggleBuildMenu(false);
        }

        private void OpenBuildMenu()
        {
            _buildMenuRoot.IsOpen = true;
            _buildMenuListView.RefreshItems();
            OnToggleBuildMenu(true);
        }

        public void RegisterEventOnPriorityTargets<TEvent>(EventCallback<TEvent> evt)
            where TEvent : EventBase<TEvent>, new()
        {
            foreach ( var priorityClickEventTarget in _priorityTargetsForEvents )
            {
                priorityClickEventTarget.RegisterCallback(evt, TrickleDown.TrickleDown);
            }
        }

        public void UnregisterEventOnPriorityTargets<TEvent>(EventCallback<TEvent> evt)
            where TEvent : EventBase<TEvent>, new()
        {
            foreach ( var priorityClickEventTarget in _priorityTargetsForEvents )
            {
                priorityClickEventTarget.UnregisterCallback(evt);
            }
        }

        private static void SetImageBackground(VisualElement element, Sprite displayIcon)
        {
            element.style.backgroundImage = new StyleBackground(displayIcon);
        }

        public void SyncCustomizationUIFieldsWithLocalPlayerCharacter(NativeArray<NativeReference<ChangeTracker<int>>> skinIndexTrackers, NativeArray<NativeReference<ChangeTracker<int>>> skinColorIndexTrackers)
        {
#if UNITY_EDITOR
            if ( characterAnimator && !UnityEditor.PrefabUtility.IsPartOfPrefabAsset(characterAnimator) )
            {
                Destroy(characterAnimator);
            }
#endif
            Assert.AreEqual(skinIndexTrackers.Length, _characterCreatorData.Categories.Length);
            Assert.AreEqual(skinIndexTrackers.Length, skinColorIndexTrackers.Length);
            for ( int i = 0; i < _characterCreatorData.Categories.Length; i++ )
            {
                var uiCustom = _allCustomizationUIFields[i];
                uiCustom.Initialize(skinIndexTrackers[i], skinColorIndexTrackers[i]);
#if UNITY_EDITOR
                uiCustom.CharacterAnimationRoot = null;
#endif
            }
        }

        private void OnPlayerActionHover(MouseEnterEvent evt)
        {
            _iconPlayerActions.style.backgroundImage = new StyleBackground(PlayerActionHoveredSprite);
        }

        private void OnPlayerActionHoverExit(MouseLeaveEvent evt)
        {
            _iconPlayerActions.style.backgroundImage = new StyleBackground(PlayerActionDefaultSprite);
        }

        private void OnPlayerActionClick(MouseDownEvent evt)
        {
            _expandedPlayerActionsRoot.IsOpen = !_expandedPlayerActionsRoot.IsOpen;
            _buttonPlayerActions.style.backgroundImage = new StyleBackground(ButtonPressedSprite);
        }

        private void OnPlayerActionClickReleased(MouseUpEvent evt)
        {
            _buttonPlayerActions.style.backgroundImage = new StyleBackground(ButtonDefaultSprite);
            CloseAll();
        }

        private void OnSettingsHover(MouseEnterEvent evt)
        {
            _iconSettings.style.backgroundImage = new StyleBackground(SettingsHoveredSprite);
        }

        private void OnSettingsHoverExit(MouseLeaveEvent evt)
        {
            _iconSettings.style.backgroundImage = new StyleBackground(SettingsDefaultSprite);
        }

        private void OnSettingsClick(MouseDownEvent evt)
        {
            _buttonSettings.style.backgroundImage = new StyleBackground(ButtonPressedSprite);
            _settingsRoot.IsOpen = !_settingsRoot.IsOpen;
        }

        private void OnSettingsReleased(MouseUpEvent evt)
        {
            _buttonCharacterCreator.style.backgroundImage = new StyleBackground(ButtonDefaultSprite);
        }

        private void OnCharacterCreatorHover(MouseEnterEvent evt)
        {
            _iconCharacterCreator.style.backgroundImage = new StyleBackground(CharacterCreatorHoveredSprite);
        }

        private void OnCharacterCreatorHoverExit(MouseLeaveEvent evt)
        {
            _iconCharacterCreator.style.backgroundImage = new StyleBackground(CharacterCreatorDefaultSprite);
        }

        private void OnCharacterCreatorClick(MouseDownEvent evt)
        {
            _buttonCharacterCreator.style.backgroundImage = new StyleBackground(ButtonPressedSprite);
            IsCharacterCreatorOpen = !IsCharacterCreatorOpen;
        }

        private void OnCharacterCreatorReleased(MouseUpEvent evt)
        {
            _buttonCharacterCreator.style.backgroundImage = new StyleBackground(ButtonDefaultSprite);
        }

        private void CloseAll()
        {
            IsCharacterCreatorOpen = false;
            _settingsRoot.IsOpen = false;
        }


        private void OnExitReleased(PointerUpEvent evt)
        {
            _iconExit.style.backgroundImage = new StyleBackground(ButtonExitDefaultSprite);
            CloseAll();
        }

        private void OnExitClick(PointerDownEvent evt)
        {
            _iconExit.style.backgroundImage = new StyleBackground(ButtonExitPressedSprite);
        }

        private void OnAvatarSelectionReleased(MouseUpEvent evt)
        { }

        private void OnAvatarSelectionClick(MouseDownEvent evt)
        {
            _iconAvatarSelection.Value.style.backgroundImage = new StyleBackground(ButtonAvatarSelectedSprite);
            _iconOutfitSelection.Value.style.backgroundImage = new StyleBackground(ButtonOutfitUnselectedSprite);
            _bodySelections.IsOpen = true;
            _faceSelections.IsOpen = true;
            _outfitSelections.IsOpen = false;
        }

        private void OnOutfitSelectionReleased(MouseUpEvent evt)
        { }

        private void OnOutfitSelectionClick(MouseDownEvent evt)
        {
            _iconOutfitSelection.style.backgroundImage = new StyleBackground(ButtonOutfitSelectedSprite);
            _iconAvatarSelection.style.backgroundImage = new StyleBackground(ButtonAvatarUnselectedSprite);
            _bodySelections.style.display = DisplayStyle.None;
            _faceSelections.style.display = DisplayStyle.None;
            _outfitSelections.style.display = DisplayStyle.Flex;
        }

        private void OnDisable()
        {
            _buttonPlayerActions.UnregisterCallback<MouseEnterEvent>(OnPlayerActionHover);
            _buttonPlayerActions.UnregisterCallback<MouseLeaveEvent>(OnPlayerActionHoverExit);
            _buttonPlayerActions.UnregisterCallback<MouseDownEvent>(OnPlayerActionClick);
            _buttonPlayerActions.UnregisterCallback<MouseUpEvent>(OnPlayerActionClickReleased);

            _buttonSettings.UnregisterCallback<MouseEnterEvent>(OnSettingsHover);
            _buttonSettings.UnregisterCallback<MouseLeaveEvent>(OnSettingsHoverExit);
            _buttonSettings.UnregisterCallback<MouseDownEvent>(OnSettingsClick);
            _buttonSettings.UnregisterCallback<MouseUpEvent>(OnSettingsReleased);

            _buttonCharacterCreator.UnregisterCallback<MouseEnterEvent>(OnCharacterCreatorHover);
            _buttonCharacterCreator.UnregisterCallback<MouseLeaveEvent>(OnCharacterCreatorHoverExit);
            _buttonCharacterCreator.UnregisterCallback<MouseDownEvent>(OnCharacterCreatorClick);
            _buttonCharacterCreator.UnregisterCallback<MouseUpEvent>(OnCharacterCreatorReleased);

            _buttonOutfitSelection.UnregisterCallback<MouseDownEvent>(OnOutfitSelectionClick);
            _buttonOutfitSelection.UnregisterCallback<MouseUpEvent>(OnOutfitSelectionReleased);

            _buttonAvatarSelection.UnregisterCallback<MouseDownEvent>(OnAvatarSelectionClick);
            _buttonAvatarSelection.UnregisterCallback<MouseUpEvent>(OnAvatarSelectionReleased);

            _buttonExit.UnregisterCallback<PointerDownEvent>(OnExitClick);
            _buttonExit.UnregisterCallback<PointerUpEvent>(OnExitReleased);
            _buildMenuListViewContentRoot.UnregisterCallback<ClickEvent>(OnSelectedBuildObjectWrapper);
        }

        public void DeselectBuildObject()
        {
            _buildMenuListView.ClearSelection();
        }
    }

    public interface IRequireInputAsset
    {
        void SetInputProvider(GameInputsAsset inputAsset);
    }

    public interface IDocumentUIProvider : IRequireInputAsset
    {
        public UIDocument Document { get; }

        public void RegisterEventOnPriorityTargets<TEvent>(EventCallback<TEvent> evt)
            where TEvent : EventBase<TEvent>, new();

        public void UnregisterEventOnPriorityTargets<TEvent>(EventCallback<TEvent> evt)
            where TEvent : EventBase<TEvent>, new();
    }

}