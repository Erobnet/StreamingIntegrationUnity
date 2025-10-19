#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameProject.UI
{
    public class DragAndDropProjectObjectsManipulator<TObject> : PointerManipulator
        where TObject : Object
    {
        // The stored asset object, if any.
        private List<TObject> _droppedObjects = null;
        // The path of the stored asset, or the empty string if there isn't one.

        public DragAndDropProjectObjectsManipulator(VisualElement root)
        {
            // The target of the manipulator, the object to which to register all callbacks, is the drop area.
            target = root.Q<VisualElement>(className: "drop-area");
        }


        public IReadOnlyList<TObject> DroppedObjects => _droppedObjects;

        protected override void RegisterCallbacksOnTarget()
        {
            // Register callbacks for various stages in the drag process.
            target.RegisterCallback<DragEnterEvent>(OnDragEnter);
            target.RegisterCallback<DragLeaveEvent>(OnDragLeave);
            target.RegisterCallback<DragUpdatedEvent>(OnDragUpdate);
            target.RegisterCallback<DragPerformEvent>(OnDragPerform);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            // Unregister all callbacks that you registered in RegisterCallbacksOnTarget().
            target.UnregisterCallback<DragEnterEvent>(OnDragEnter);
            target.UnregisterCallback<DragLeaveEvent>(OnDragLeave);
            target.UnregisterCallback<DragUpdatedEvent>(OnDragUpdate);
            target.UnregisterCallback<DragPerformEvent>(OnDragPerform);
        }

        // This method runs if a user brings the pointer over the target while a drag is in progress.
        void OnDragEnter(DragEnterEvent _)
        {

            // Change the appearance of the drop area if the user drags something over the drop area and holds it
            // there.
            target.AddToClassList("drop-area--dropping");
        }

        // This method runs if a user makes the pointer leave the bounds of the target while a drag is in progress.
        void OnDragLeave(DragLeaveEvent _)
        {
            target.RemoveFromClassList("drop-area--dropping");
        }

        // This method runs every frame while a drag is in progress.
        void OnDragUpdate(DragUpdatedEvent _)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
        }

        // This method runs when a user drops a dragged object onto the target.
        void OnDragPerform(DragPerformEvent _)
        {
            // Set droppedObject and draggedName fields to refer to dragged object.
            if ( _droppedObjects == null )
            {
                _droppedObjects = new List<TObject>(DragAndDrop.objectReferences.Length);
            }
            else
            {
                _droppedObjects.Clear();
            }
            foreach ( var objectReference in DragAndDrop.objectReferences )
            {
                if ( objectReference is TObject obj )
                {
                    _droppedObjects.Add(obj);
                }
            }

            // Visually update target to indicate that it now stores an asset.
            target.RemoveFromClassList("drop-area--dropping");
        }
    }
}
#endif