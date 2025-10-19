using System;
using System.Collections;
using System.Runtime.CompilerServices;
using GameProject.Persistence.CommonData;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = Unity.Assertions.Assert;

public class SerializationTests
{
    [Test]
    public unsafe void SerializeAndDeserializeCharacterViewerDataInMemoryTest()
    {
        // // Use the Assert class to test conditions
        AllocatorManager.AllocatorHandle tempAllocatorHandle = Allocator.Temp;
        var streamBytes = new NativeList<byte>(tempAllocatorHandle);
        var serializationCtx = new SerializationContext(streamBytes);
        var characterDataSource = new ChatUserPropertiesPersistence {
            Currency = 123,
            SkinOptions = new() {
                Indices = new() {
                    1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 30
                }
            },
            SkinColorOptions = new() {
                Indices = new() {
                    31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44
                }
            }
        };
        
        int oldPosition = 0;
        int currencySerializedSize = ProcessSerialization<GameCurrency.BinaryAdapter, GameCurrency>(ref oldPosition, ref serializationCtx, characterDataSource.Currency);
        int skinIndicesTotalSize = ProcessSerialization<SkinOptions.BinaryAdapter, SkinOptions>(ref oldPosition, ref serializationCtx, characterDataSource.SkinOptions);
        int colorsTotalSize = ProcessSerialization<SkinColorOptions.BinaryAdapter, SkinColorOptions>(ref oldPosition, ref serializationCtx, characterDataSource.SkinColorOptions);
        Assert.AreEqual(currencySerializedSize + skinIndicesTotalSize + colorsTotalSize, serializationCtx.Length);

        var deserializationCtx = new DeserializationContext(streamBytes.GetUnsafePtr(), serializationCtx.Length);
        var deserializedCharacter = new ChatUserPropertiesPersistence();
        default(GameCurrency.BinaryAdapter).Deserialize(ref deserializationCtx, &deserializedCharacter.Currency, 1);
        default(SkinOptions.BinaryAdapter).Deserialize(ref deserializationCtx, &deserializedCharacter.SkinOptions, 1);
        default(SkinColorOptions.BinaryAdapter).Deserialize(ref deserializationCtx, &deserializedCharacter.SkinColorOptions, 1);
        
        Assert.AreEqual(characterDataSource.Currency, deserializedCharacter.Currency);
        AssertCollectionHaveSameElementInTheSameOrder(characterDataSource.SkinOptions.Indices, deserializedCharacter.SkinOptions.Indices);
        AssertCollectionHaveSameElementInTheSameOrder(characterDataSource.SkinColorOptions.Indices, deserializedCharacter.SkinColorOptions.Indices);
    }

    private static int ProcessSerialization<TAdapter, T>(ref int oldPosition, ref SerializationContext serializationCtx, T data)
        where TAdapter : unmanaged, ICustomBinarySerializer<T>
        where T : unmanaged
    {
        default(TAdapter).SerializeOne(ref serializationCtx, data);
        int serializedSize = serializationCtx.Length - oldPosition;
        oldPosition = serializationCtx.Length;
        return serializedSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AssertCollectionHaveSameElementInTheSameOrder<T>(FixedList32Bytes<T> skinOptionsIndicesSrc, FixedList32Bytes<T> deserializedSkinOptionsIndices)
        where T : unmanaged, IEquatable<T>
    {
        Assert.AreEqual(skinOptionsIndicesSrc.Length, deserializedSkinOptionsIndices.Length);
        for ( var index = 0; index < skinOptionsIndicesSrc.Length; index++ )
        {
            Assert.AreEqual(skinOptionsIndicesSrc[index], deserializedSkinOptionsIndices[index]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AssertCollectionHaveSameElementInTheSameOrder<T>(FixedList64Bytes<T> skinOptionsIndicesSrc, FixedList64Bytes<T> deserializedSkinOptionsIndices)
        where T : unmanaged, IEquatable<T>
    {
        Assert.AreEqual(skinOptionsIndicesSrc.Length, deserializedSkinOptionsIndices.Length);
        for ( var index = 0; index < skinOptionsIndicesSrc.Length; index++ )
        {
            Assert.AreEqual(skinOptionsIndicesSrc[index], deserializedSkinOptionsIndices[index]);
        }
    }
}