using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using OnUIClickEventType = UnityEngine.UIElements.PointerDownEvent;

namespace GameProject.Inputs
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(BeginSimulationEntityCommandBufferSystem))]
    public partial class InputSystemCoordinator : SystemBase
    {
        private readonly List<InputActionCallback> _actionsOnClick = new();
        private readonly HashSet<InputActionCallback> _actionsOnClickLookup = new();
        private bool _hasClicked;
        private VisualElement _clickedOnElement;
        private IDocumentUIProvider _documentUIProvider;
        private ClickInputContext _clickContext;
        private readonly List<IRequireInputAsset> _requiredInputComponentsBuffer = new();
        private readonly List<GameObject> _rootGameObjectsBuffer = new();
        public GameInputsAsset GameInputsAsset {
            get;
            private set;
        }

        public void AddOnClickEvent(InputActionCallback.ClickActionDelegate actionCallback, int priorityIndex = 0)
        {
            var inputActionCallback = new InputActionCallback {
                Execute = actionCallback,
                SortIndex = priorityIndex
            };
            var added = _actionsOnClickLookup.Add(inputActionCallback);
            if ( added )
            {
                _actionsOnClick.Add(inputActionCallback);
                _actionsOnClick.Sort();
            }
        }

        public void RemoveOnClickEvent(InputActionCallback.ClickActionDelegate actionCallback)
        {
            var inputActionCallback = new InputActionCallback { Execute = actionCallback };
            bool contains = _actionsOnClickLookup.Remove(inputActionCallback);
            if ( contains )
            {
                _actionsOnClick.Remove(inputActionCallback);
            }
        }

        protected override void OnCreate()
        {
            GameInputsAsset = new();
            GameInputsAsset.UI.Click.performed += InvokeOnClickEvent;
            GameInputsAsset.UI.Point.performed += OnPointerMove;
        }

        private void OnPointerMove(InputAction.CallbackContext context)
        {
            _clickContext.ClickPosition = context.ReadValue<Vector2>();
        }

        private void InvokeOnClickEvent(InputAction.CallbackContext context)
        {
            if ( context.ReadValueAsButton() )
                _hasClicked = true;
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            GameInputsAsset.Enable();
            InitializeSceneInputRequirement();
        }

        private void InitializeSceneInputRequirement()
        {
            if ( _documentUIProvider is not null )
                return;

            Scene activeScene = SceneManager.GetActiveScene();
            activeScene.GetRootGameObjects(_rootGameObjectsBuffer);
            for ( var index = 0; index < _rootGameObjectsBuffer.Count; index++ )
            {
                GameObject rootGameObject = _rootGameObjectsBuffer[index];
                _documentUIProvider = rootGameObject.GetComponentInChildren<IDocumentUIProvider>();
                if ( _documentUIProvider is not null )
                    break;
            }
            if ( _documentUIProvider is not null )
            {
                _documentUIProvider.RegisterEventOnPriorityTargets<OnUIClickEventType>(ClickContextClickedVisualElement);
            }
            for ( var index = 0; index < _rootGameObjectsBuffer.Count; index++ )
            {
                GameObject rootGameObject = _rootGameObjectsBuffer[index];
                rootGameObject.GetComponents(_requiredInputComponentsBuffer);
                for ( var i = 0; i < _requiredInputComponentsBuffer.Count; i++ )
                {
                    var requireInputAsset = _requiredInputComponentsBuffer[i];
                    {
                        requireInputAsset.SetInputProvider(GameInputsAsset);
                    }
                }
            }
            FindAndSetUIInputsAsset();
        }

        private void FindAndSetUIInputsAsset()
        {
            for ( var index = 0; index < _rootGameObjectsBuffer.Count; index++ )
            {
                var rootGameObject = _rootGameObjectsBuffer[index];
                if ( rootGameObject.TryGetComponent(out InputSystemUIInputModule inputSystemUIInputModule) )
                {
                    inputSystemUIInputModule.actionsAsset = GameInputsAsset.asset;
                    break;
                }
            }
        }

        private void ClickContextClickedVisualElement(OnUIClickEventType evt)
        {
            _clickContext.ClickedVisualElement = evt.target as VisualElement;
        }

        protected override void OnUpdate()
        {
            InitializeSceneInputRequirement();
            if ( _hasClicked )
            {
                var stopPropagation = false;
                int? lastIndex = null;
                for ( var index = 0; index < _actionsOnClick.Count; index++ )
                {
                    var inputActionCallback = _actionsOnClick[index];
                    if ( lastIndex.HasValue && lastIndex.Value != inputActionCallback.SortIndex && stopPropagation )
                        break;

                    stopPropagation |= inputActionCallback.Execute.Invoke(in _clickContext);
                    lastIndex = inputActionCallback.SortIndex;
                }
                _clickContext.ClickedVisualElement = null;
                _hasClicked = false;
            }
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            GameInputsAsset.Disable();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _documentUIProvider?.UnregisterEventOnPriorityTargets<OnUIClickEventType>(ClickContextClickedVisualElement);
            GameInputsAsset.UI.Click.performed -= InvokeOnClickEvent;
            GameInputsAsset.UI.Point.performed -= OnPointerMove;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="world"></param>
        /// <param name="actionCallback">return whatever the input should keep propagating or not</param>
        /// <param name="priorityIndex">higher <see cref="priorityIndex"/> has priority over a lower value</param>
        /// <returns></returns>
        public static InputSystemCoordinator AddOnClickEvent(World world, InputActionCallback.ClickActionDelegate actionCallback, int priorityIndex = 0)
        {
            var coordinator = world.GetOrCreateSystemManaged<InputSystemCoordinator>();
            coordinator.AddOnClickEvent(actionCallback, priorityIndex);
            return coordinator;
        }

        public struct ClickInputContext
        {
            public Vector2 ClickPosition;
            public VisualElement ClickedVisualElement;
            public bool HasClickedOnUI => ClickedVisualElement != null;
        }

        public struct InputActionCallback : IEquatable<InputActionCallback>, IComparable<InputActionCallback>, IComparer<InputActionCallback>
        {
            public delegate bool ClickActionDelegate(in ClickInputContext position);

            public ClickActionDelegate Execute;
            public int SortIndex;

            public bool Equals(InputActionCallback other)
            {
                return Execute == other.Execute;
            }

            public override bool Equals(object obj)
            {
                return obj is InputActionCallback other && Equals(other);
            }

            public override int GetHashCode()
            {
                return Execute.GetHashCode();
            }

            public int CompareTo(InputActionCallback other)
            {
                return -SortIndex.CompareTo(other.SortIndex);
            }

            public static bool operator ==(InputActionCallback left, InputActionCallback right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(InputActionCallback left, InputActionCallback right)
            {
                return !left.Equals(right);
            }

            public int Compare(InputActionCallback x, InputActionCallback y)
            {
                return x.CompareTo(y);
            }
        }
    }
}