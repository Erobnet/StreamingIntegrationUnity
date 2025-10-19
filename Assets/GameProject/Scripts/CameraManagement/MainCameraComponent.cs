using UnityEngine;

namespace GameProject.CameraManagement
{
    public class MainCameraComponent : MonoBehaviour
    {
        public static Camera Main {
            get;
            private set;
        }

        private void Awake()
        {
            if ( Main != null )
            {
                Debug.LogError($"a camera main {Main.name} already exists but {gameObject.name} is trying to overwrite it.");
                return;
            }
            Main = GetComponent<Camera>();
        }

        private void OnDestroy()
        {
            if ( Main == GetComponent<Camera>() )
            {
                Main = null;
            }
        }
    }
}