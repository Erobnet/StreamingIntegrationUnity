using System.Collections.Generic;
using UnityEngine;

namespace GameProject
{
    public class StringLightsController : MonoBehaviour
    {
        [Header("Lights Settings")]
        public Transform parentObject;  // Assign the parent object containing point lights here through the inspector.
        public float minIntensity = 0.0f;  // Minimum brightness for the lights.
        public float maxIntensity = 1.5f;  // Maximum brightness for the lights.
        public float fadeSpeed = 1.0f;  // Speed at which lights fade in and out.

        private List<Light> pointLights = new List<Light>();
        private float[] randomOffsets;

        void Start()
        {
            if (parentObject == null)
            {
                Debug.LogWarning("No parent object assigned to ChristmasLightsController");
                return;
            }

            // Find all point lights that are children of the parent object
            pointLights.AddRange(parentObject.GetComponentsInChildren<Light>());

            if (pointLights.Count == 0)
            {
                Debug.LogWarning("No point lights found under the parent object");
                return;
            }
        }

        void Update()
        {
            for (int i = 0; i < pointLights.Count; i++)
            {
                if (pointLights[i] != null)
                {
                    float phaseOffset = (i % 2 == 0) ? 0f : Mathf.PI;  // Every other light has a phase offset of PI
                    float intensity = Mathf.Lerp(minIntensity, maxIntensity, (Mathf.Sin(Time.time * fadeSpeed + phaseOffset) + 1f) / 2f);
                    pointLights[i].intensity = intensity;
                }
            }
        }
    }
}
