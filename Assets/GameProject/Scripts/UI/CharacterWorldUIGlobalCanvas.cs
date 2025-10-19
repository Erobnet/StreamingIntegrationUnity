using System.Collections.Generic;
using UnityEngine;

namespace GameProject.UI
{
    public class CharacterWorldUIGlobalCanvas : MonoBehaviour
    {
        private List<WorldCharacterUI> _uiControllers = new();
        private List<GameObject> _uiControllersGameObjects = new();

        private void Awake()
        {
            _uiControllers = new();
            _uiControllersGameObjects = new();
            foreach ( Transform child in transform )
            {
                Destroy(child.gameObject);
            }
            transform.DetachChildren();
        }

        private void Update()
        {
            for ( var index = _uiControllers.Count - 1; index >= 0; index-- )
            {
                var uiControllersGameObject = _uiControllersGameObjects[index];
                if ( uiControllersGameObject.activeSelf )
                {
                    var worldUIController = _uiControllers[index];
                    worldUIController.ProcessTextUpdate();
                }
            }
        }

        public void UpdateHierarchy()
        {
            _uiControllers.Clear();
            _uiControllersGameObjects.Clear();
            //this allows to only check on the direct children of the script gameobject only
            for ( int i = 0; i < transform.childCount; i++ )
            {
                var directChildGameObject = transform.GetChild(i).gameObject;
                //include only active objects, disabled gameobject at the hierarchy level of the CharacterWorldUIController should only be disabled by the pool meaning that we must not use them  
                if ( directChildGameObject.activeSelf && directChildGameObject.TryGetComponent(out WorldCharacterUI characterWorldUIController) )
                {
                    _uiControllers.Add(characterWorldUIController);
                    _uiControllersGameObjects.Add(directChildGameObject);
                }
            }
        }
    }
}