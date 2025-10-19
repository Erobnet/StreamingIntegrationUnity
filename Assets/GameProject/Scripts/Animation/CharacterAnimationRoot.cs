using System.Collections.Generic;
using GameProject.Characters;
using Unity.Assertions;
using Unity.Mathematics;
using UnityEngine;

namespace GameProject.Animation
{
    public class CharacterAnimationRoot : MonoBehaviour
    {
        private static readonly int _MoveXParameterHash = Animator.StringToHash("MoveX");
        private static readonly int _MoveYParameterHash = Animator.StringToHash("MoveY");
        private static readonly int _MovementStateParameterHash = Animator.StringToHash("MovementState");
        private static readonly int _FacingXParameterHash = Animator.StringToHash("FacingX");
        private static readonly int _FacingZParameterHash = Animator.StringToHash("FacingZ");

        [SerializeField] private List<AnimationPartRoot> childAnimators;

        private void OnValidate()
        {
            if ( Application.isPlaying )
                return;

            var oldLength = childAnimators.Count;

            GetComponentsInChildren(true, childAnimators); //this method will clear the list
            if ( TryGetComponent(out AnimationPartRoot animatorParent) )
            {
                childAnimators.Add(animatorParent);
            }
#if UNITY_EDITOR
            if ( oldLength != childAnimators.Count )
            {
                this.SetDirtySafe();
            }
#endif
        }

        public void SetSpriteRendererMaterial(CharacterPartSlotID part, Material material)
        {
            var partAnimator = childAnimators[part.GetIndex()];
            if ( partAnimator.SpriteRenderer.material != material )
                partAnimator.SpriteRenderer.material = material;
        }

        public bool TrySetSpriteLibrarySlot(PartSlot part)
        {
            AnimationPartRoot partAnimator = SetSpriteLibrarySlotImpl(part);
            return partAnimator.SpriteLibrary.spriteLibraryAsset != part.PartAsset;
        }

        public void SetSpriteLibrarySlot(PartSlot part)
        {
            SetSpriteLibrarySlotImpl(part);
        }

        private AnimationPartRoot SetSpriteLibrarySlotImpl(PartSlot part)
        {
            var partAnimator = childAnimators[part.SlotID.GetIndex()];
            partAnimator.SpriteLibrary.spriteLibraryAsset = part.PartAsset;
            return partAnimator;
        }

        public void ResyncAnimation()
        {
            Animator animator = childAnimators[0].Animator;
            for ( int i = 0; i < animator.layerCount; i++ )
            {
                AnimatorStateInfo currentAnimatorStateInfo = animator.GetCurrentAnimatorStateInfo(i);

                int currentStateHash = currentAnimatorStateInfo.shortNameHash;
                for ( var index = 0; index < childAnimators.Count; index++ )
                {
                    AnimationPartRoot childAnimation = childAnimators[index];
#if UNITY_EDITOR
                    Assert.IsTrue(childAnimation.Animator.runtimeAnimatorController == animator.runtimeAnimatorController);
#endif
                    childAnimation.Animator.Play(currentStateHash, i, currentAnimatorStateInfo.normalizedTime);
                }
            }
        }

        public void UpdateAnimators(float3 velocity,float2 directionFacing, bool isSitting)
        {
            int movementState = 0;
            if ( isSitting )
                movementState = 2;

            for ( var index = 0; index < childAnimators.Count; index++ )
            {
                var animator = childAnimators[index].Animator;
                animator.SetFloat(_MoveXParameterHash, velocity.x);
                animator.SetFloat(_MoveYParameterHash, velocity.z);
                animator.SetFloat(_FacingXParameterHash, directionFacing.x);
                animator.SetFloat(_FacingZParameterHash, directionFacing.y);
                CheckForChangeThenSet(animator, _MovementStateParameterHash, movementState);
            }
        }

        public static void CheckForChangeThenSet(Animator animator, int parameterHash, int value)
        {
            if ( animator.GetInteger(parameterHash) != value )
            {
                animator.SetInteger(parameterHash, value);
            }
        }
    }
}