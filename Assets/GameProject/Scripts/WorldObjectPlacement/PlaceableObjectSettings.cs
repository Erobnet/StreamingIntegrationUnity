using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameProject.WorldObjectPlacement
{
    [Serializable]
    public struct Vector2XZ
    {
        public float x;
        public float z;
    }

    [CreateAssetMenu(fileName = nameof(PlaceableObjectSettings), menuName = GameProjectHelper.GAME_PROJECT_ASSET_MENU + nameof(PlaceableObjectSettings))]
    public class PlaceableObjectSettings : ScriptableObject
    {
        [SerializeField] private Vector3 _startPoint;
        [SerializeField] private Vector3[] _snapGridCellSizes;
        [SerializeField] private PlaceableObjectIndexer[] placeableObjectPrefabs;
        [SerializeField] private LayerMask placementCollisionMasks;
        [SerializeField] private LayerMask surfaceCollisionMasks;
        [SerializeField] private LayerMask moveableCollisionMask;
        [SerializeField] private Color obstructedPlacementColor = Color.red;
        [SerializeField] public Color SelectableObjectColor = Color.green;

        public Vector3 StartPoint => _startPoint;
        public Vector3[] SnapCellSizes => _snapGridCellSizes;
        public IReadOnlyList<PlaceableObjectIndexer> PlaceableObjectPrefabs => placeableObjectPrefabs;
        public LayerMask PlacementCollisionMasks => placementCollisionMasks;
        public LayerMask MoveableCollisionMask => moveableCollisionMask;
        public LayerMask SurfaceCollisionMasks => surfaceCollisionMasks;
        public Color ObstructedPlacementColor => obstructedPlacementColor;
    }
}