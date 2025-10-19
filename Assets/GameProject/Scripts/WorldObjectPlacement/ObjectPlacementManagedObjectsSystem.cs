using System;
using GameProject.Characters;
using GameProject.Inputs;
using GameProject.Persistence.CommonData;
using GameProject.Player;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Extensions;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GameProject.WorldObjectPlacement
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(BeginSimulationEntityCommandBufferSystem))]
    public partial class ObjectPlacementManagedObjectsSystem : SystemBase
    {
        private GlobalUIController _globalUIController;
        private int _selectedBuildObjectIndex;
        private bool _onSelectBuildObject;
        private ChangeTracker<bool> _toggleBuildMenu;
        private EntityQuery _localPlayerPlaceablePrefabQuery;
        private EntityQuery _buildInputQuery;
        private InputSystemCoordinator _inputSystemCoordinator;
        private bool _clickPerformed;
        private bool _deletePerformed;
        private bool _confirmMovePerformed;
        private bool _cancelInputPerformed;
        private bool _rotateInputPerformed;
        private Entity _buildInputDataEntity;
        private NativeList<BuildObjectViewData> _buildObjectDatas;
        private Entity _buildObjectCollectionEntity;
        private EntityQuery _changeTrackedQuantityQuery;
        private EntityQuery _playerQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            _buildObjectDatas = new(16, Allocator.Persistent);
            _changeTrackedQuantityQuery = SystemAPI.QueryBuilder()
                .WithAll<TrackedQuantity>()
                .Build();
            _changeTrackedQuantityQuery.SetChangedVersionFilter<TrackedQuantity>();
            _localPlayerPlaceablePrefabQuery = SystemAPI.QueryBuilder()
                .WithAll<PlacementSettingsData, BuildObjectCollection>()
                .WithOptions(EntityQueryOptions.IncludePrefab)
                .Build();
            RequireForUpdate(_localPlayerPlaceablePrefabQuery);
            _inputSystemCoordinator = World.GetOrCreateSystemManaged<InputSystemCoordinator>();
            _buildInputQuery = SystemAPI.QueryBuilder()
                .WithPresent<BuildInputData>()
                .Build();
            var entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp);
            _playerQuery = entityQueryBuilder
                .WithAll<LocalPlayerTag, GameCurrency>()
                .Build(ref CheckedStateRef);
        }

        private void OnDeletePerformed(InputAction.CallbackContext ctx)
        {
            SetValueOnlyIfTrue(ctx, ref _deletePerformed);
        }

        private void SetValueOnlyIfTrue(InputAction.CallbackContext ctx, ref bool inputPerformed)
        {
            if ( ctx.ReadValueAsButton() )
                inputPerformed = true;
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            FindUIController();
            var entityArray = _buildInputQuery.ToEntityArray(Allocator.Temp);
            if ( entityArray.Length == 0 )
            {
                var buildInputArchetype = EntityManager.CreateArchetype(stackalloc ComponentType[] {
                    ComponentType.ReadWrite<BuildInputData>(),
                    ComponentType.ReadWrite<ApplyColorData>(),
                });
                _buildInputDataEntity = EntityManager.CreateEntity(buildInputArchetype);
            }
            else
            {
                _buildInputDataEntity = entityArray[0];
            }
            EntityManager.SetComponentEnabled<BuildInputData>(_buildInputDataEntity, false);
            EntityManager.SetComponentEnabled<ApplyColorData>(_buildInputDataEntity, false);
        }

        public void RegisterInputEvents()
        {
            _inputSystemCoordinator.AddOnClickEvent(OnClickPerformed, 10);
            _inputSystemCoordinator.GameInputsAsset.UI.Delete.performed += OnDeletePerformed;
            _inputSystemCoordinator.GameInputsAsset.UI.Cancel.performed += OnCancelInputPerformed;
            _inputSystemCoordinator.GameInputsAsset.UI.Rotate.performed += OnRotatePerformed;
        }

        private void OnRotatePerformed(InputAction.CallbackContext obj)
        {
            SetValueOnlyIfTrue(obj, ref _rotateInputPerformed);
        }

        private void OnCancelInputPerformed(InputAction.CallbackContext obj)
        {
            SetValueOnlyIfTrue(obj, ref _cancelInputPerformed);
        }

        public void UnregisterInputEvents()
        {
            _inputSystemCoordinator.RemoveOnClickEvent(OnClickPerformed);
            _inputSystemCoordinator.GameInputsAsset.UI.Delete.performed -= OnDeletePerformed;
            _inputSystemCoordinator.GameInputsAsset.UI.Cancel.performed -= OnCancelInputPerformed;
            _inputSystemCoordinator.GameInputsAsset.UI.Rotate.performed -= OnRotatePerformed;
        }

        public bool OnClickPerformed(in InputSystemCoordinator.ClickInputContext context)
        {
            var buildInputDataLookup = SystemAPI.GetComponentLookup<BuildInputData>();
            _clickPerformed = !context.HasClickedOnUI
                              && buildInputDataLookup.TryGetComponent(_buildInputDataEntity, out var buildInputData)
                              && (buildInputData.SelectedEntity != Entity.Null || buildInputData.SelectedEntityForMove != Entity.Null);

            return _clickPerformed;
        }

        private void FindUIController()
        {
            if ( _globalUIController )
            {
                CheckForBuildObjectListUpdate();
                return;
            }

            _globalUIController = SceneManager.GetActiveScene().FindFirstInstancesInScene<GlobalUIController>();
            if ( _globalUIController )
            {
                _globalUIController.OnToggleBuildMenu += OnToggleBuildMenu;
                _globalUIController.OnSelectedBuildObject += OnSelectBuildObject;
                _globalUIController.SetBuildMenuItemSource(UpdatePlaceablePrefabCollectionSource());
            }
        }

        private void OnToggleBuildMenu(bool active)
        {
            _toggleBuildMenu.Value = active;
            if ( active )
            {
                RegisterInputEvents();
            }
            else
            {
                UnregisterInputEvents();
            }
        }

        private void OnSelectBuildObject(int index)
        {
            _selectedBuildObjectIndex = index;
            _onSelectBuildObject = true;
        }

        private void CheckForBuildObjectListUpdate()
        {
            bool hasCollection = !_localPlayerPlaceablePrefabQuery.IsEmptyIgnoreFilter;

            _localPlayerPlaceablePrefabQuery.SetChangedVersionFilter<BuildObjectCollection>();
            bool collectionUpdateIsAvailable = !_localPlayerPlaceablePrefabQuery.IsEmpty;
            _localPlayerPlaceablePrefabQuery.ResetFilter();

            bool trackedQuantityChanged = !_changeTrackedQuantityQuery.IsEmpty;
            _playerQuery.SetChangedVersionFilter<GameCurrency>();
            bool playerJustArrivedOrMoneyChanged = !_playerQuery.IsEmpty;
            _playerQuery.ResetFilter();
            if ( playerJustArrivedOrMoneyChanged || collectionUpdateIsAvailable || (hasCollection && trackedQuantityChanged) )
            {
                _globalUIController.SetBuildMenuItemSource(UpdatePlaceablePrefabCollectionSource());
            }
        }

        private Entity UpdateEntityBuildCollectionSource()
        {
            return _buildObjectCollectionEntity = _localPlayerPlaceablePrefabQuery.GetSingletonEntity();
        }

        protected override void OnUpdate()
        {
            FindUIController();

            var entityArray = _buildInputQuery.ToEntityArray(Allocator.Temp);
            if ( entityArray.Length == 0 )
                return;

            Entity buildInputDataEntity = entityArray[0];
            ref BuildInputData buildInputData = ref EntityManager.GetComponentDataRW<BuildInputData>(buildInputDataEntity).ValueRW;
            buildInputData.ClickInputPerformed = _clickPerformed;
            buildInputData.DeleteInputPerformed = _deletePerformed;
            buildInputData.CancelInputPerformed = _cancelInputPerformed;
            buildInputData.RotateBuildPerformed = _rotateInputPerformed;
            if ( _cancelInputPerformed )
            {
                _globalUIController.DeselectBuildObject();
            }
            _rotateInputPerformed = false;
            _deletePerformed = false;
            _clickPerformed = false;
            _cancelInputPerformed = false;

            bool toggleBuildMenuHasChanged = _toggleBuildMenu.UpdateHasChanged();
            if ( !(toggleBuildMenuHasChanged || _onSelectBuildObject) )
                return;

            if ( toggleBuildMenuHasChanged )
            {
                EntityManager.SetComponentEnabled<BuildInputData>(buildInputDataEntity, _toggleBuildMenu.Value);
            }

            HandleBuildObjectSelectionChanged(ref buildInputData);
        }

        private void HandleBuildObjectSelectionChanged(ref BuildInputData buildInputData)
        {
            if ( _onSelectBuildObject )
            {
                _onSelectBuildObject = false;
                var allPlaceablePrefabs = EntityManager.GetBuffer<BuildObjectCollection>(_buildObjectCollectionEntity, true).Reinterpret<Entity>();
                Entity placeablePrefab = allPlaceablePrefabs[_selectedBuildObjectIndex];
                buildInputData.PrefabIndexer.Value = placeablePrefab;
            }
        }

        private ReadOnlySpan<BuildObjectViewData> UpdatePlaceablePrefabCollectionSource()
        {
            _buildObjectDatas.Clear();
            var hasPlayer = _playerQuery.TryGetSingleton<GameCurrency>(out var playerCurrency);
            var buffer = EntityManager.GetBuffer<BuildObjectCollection>(UpdateEntityBuildCollectionSource(), true);
            for ( var index = 0; index < buffer.Length; index++ )
            {
                var buildObjectCollection = buffer[index];
                Entity prefabCollectionEntity = buildObjectCollection.PrefabCollection;
                var buildObjectViewData = new BuildObjectViewData {
                    BuildObjectData = EntityManager.GetComponentData<BuildObjectData>(prefabCollectionEntity),
                    TrackedQuantity = EntityManager.HasComponent<TrackedQuantity>(prefabCollectionEntity)
                        ? EntityManager.GetComponentData<TrackedQuantity>(prefabCollectionEntity)
                        : null,
                    IsAvailable = hasPlayer && ObjectPlacementSystem.CanPlayerAffordPlaceable(playerCurrency, EntityManager.GetComponentData<GameCurrency>(prefabCollectionEntity))
                };
                if ( buildObjectViewData.TrackedQuantity.HasValue && buildObjectViewData.IsAvailable )
                    buildObjectViewData.IsAvailable = buildObjectViewData.TrackedQuantity.Value.Available > 0;
                _buildObjectDatas.Add(buildObjectViewData);
            }
            return _buildObjectDatas.AsArray().AsReadOnlySpan();
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            if ( _globalUIController )
            {
                _globalUIController.OnSelectedBuildObject -= OnSelectBuildObject;
                _globalUIController.OnToggleBuildMenu -= OnToggleBuildMenu;
                _globalUIController = null;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _buildObjectDatas.Dispose();
        }
    }

    public struct BuildObjectViewData
    {
        public BuildObjectData BuildObjectData;
        public TrackedQuantity? TrackedQuantity;
        public bool IsAvailable;
    }
}