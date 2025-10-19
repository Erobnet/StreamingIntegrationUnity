using System;
using System.Diagnostics;
using GameProject.Characters;
using Unity.Entities;
using Unity.Properties;
using UnityEngine;
using static GameProject.GameProjectHelper;

namespace GameProject.Animation
{
    public class CharacterAnimatorSpawnerAuthoring : MonoBehaviour
    {
        [SerializeField] private CharacterAnimationRootPrefabRef characterAnimationRootPrefabRef;

        private class Baker : Baker<CharacterAnimatorSpawnerAuthoring>
        {
            public override void Bake(CharacterAnimatorSpawnerAuthoring authoring)
            {
                AssertIsPartOfPrefab(authoring, authoring.characterAnimationRootPrefabRef.gameObject, nameof(CharacterAnimatorPrefab));
                var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
                AddBuffer<PersistentSkinOptionApply>(entity);
                AddBuffer<PersistentSkinColorOptionApply>(entity);
                AddBuffer<SkinOptionOverrideApply>(entity);
                AddComponent(entity, new CharacterAnimatorPrefab {
                    PrefabReference = authoring.characterAnimationRootPrefabRef,
                });
            }
        }
    }

    public interface ICharacterCategoryIndex
    {
        public CharacterOptionIndex Value { get; set; }
    }

    /// <summary>
    /// every element here will be persisted hence the separation with their temp version
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct PersistentSkinOptionApply : IBufferElementData, IEnableableComponent, ICharacterCategoryIndex
    {
        public CharacterOptionIndex Value;

        CharacterOptionIndex ICharacterCategoryIndex.Value {
            get => Value;
            set => Value = value;
        }
    }

    [InternalBufferCapacity(0)]
    public struct SkinOptionOverrideApply : IBufferElementData, IEnableableComponent
    {
        public UnityObjectRef<CharacterTransitionData> CharacterTransitionWrapper;
        public UnityObjectRef<Material> SelectedColor;
    }

    /// <summary>
    /// <inheritdoc cref="PersistentSkinOptionApply"/>
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct PersistentSkinColorOptionApply : IBufferElementData, IEnableableComponent, ICharacterCategoryIndex
    {
        public CharacterOptionIndex Value;

        CharacterOptionIndex ICharacterCategoryIndex.Value {
            get => Value;
            set => Value = value;
        }
    }

    [Serializable]
    [DebuggerDisplay("index= {_index}")]
    public struct CharacterOptionIndex
    {
        [CreateProperty] private byte _index;

        public byte Index {
            readonly get => _index;
            set => _index = value;
        }

        public static implicit operator byte(CharacterOptionIndex index) => index.Index;

        public static implicit operator CharacterOptionIndex(int index) => new CharacterOptionIndex {
            Index = (byte)index
        };
    }

    public struct CharacterAnimatorPrefab : IComponentData
    {
        public UnityObjectRef<CharacterAnimationRootPrefabRef> PrefabReference;
    }

    public class CharacterAnimatorSpawnInstance : IComponentData
    {
        public CharacterAnimationRoot Value;
        public Transform CachedTransform;
    }
}