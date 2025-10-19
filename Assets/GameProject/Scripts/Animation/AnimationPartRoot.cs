using UnityEngine;
using UnityEngine.U2D.Animation;

namespace GameProject.Animation
{
    public class AnimationPartRoot : MonoBehaviour
    {
        [SerializeField] private SpriteLibrary spriteLibrary;
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;

        public SpriteLibrary SpriteLibrary => spriteLibrary;
        public Animator Animator => animator;
        public SpriteRenderer SpriteRenderer => spriteRenderer;
#if UNITY_EDITOR
        private void OnValidate()
        {
            if ( Application.isPlaying )
                return;

            if ( !spriteLibrary )
            {
                spriteLibrary = GetComponent<SpriteLibrary>();
                animator = GetComponent<Animator>();
                this.SetDirtySafe();
            }

            if (!spriteRenderer)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
                this.SetDirtySafe();
            }
        }
#endif
    }
}