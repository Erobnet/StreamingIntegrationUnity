using UnityEngine;

namespace GameProject.Animation
{
    public class Oscillate : MonoBehaviour
    {
        [Header("Blink Settings")]
        public float blinkSpeed = 2f;
    
        [Header("Float Settings")]
        public float floatSpeed = 1f;
        public float floatAmplitude = 0.2f;

        private SpriteRenderer spriteRenderer;
        private float originalY;

        private void Start()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            originalY = transform.position.y;
        }

        private void Update()
        {
            // Make the marker blink by changing its alpha value
            Color color = spriteRenderer.color;
            color.a = Mathf.Abs(Mathf.Sin(Time.time * blinkSpeed));
            spriteRenderer.color = color;

            // Make the marker float up and down
            float newY = originalY + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
    }
}
