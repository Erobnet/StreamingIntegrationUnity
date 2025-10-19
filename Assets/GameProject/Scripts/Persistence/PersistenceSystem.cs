using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Drboum.Utilities.Collections;
using GameProject.Animation;
using GameProject.Characters;
using ChatBot.Runtime;
using GameProject.Persistence.CommonData;
using GameProject.Player;
using GameProject.WorldObjectPlacement;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Extensions;
using Unity.Entities.Internal;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Properties;
using Unity.Serialization.Json;
using Unity.Transforms;
using UnityEngine;
using ECBSynchronizationSystem = Unity.Entities.BeginInitializationEntityCommandBufferSystem;
using static GameProject.Persistence.PersistenceSystemPaths;

namespace GameProject.Persistence
{
    /// <summary>
    /// System responsible to collect and persist data to disk 
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial class PersistenceSystem : SystemBase
    {
        private static readonly ProfilerMarker _WriteWorldDataToFileScheduleCollectJobsMarker = new(nameof(_WriteWorldDataToFileScheduleCollectJobsMarker).NicifyVariable());
        private static readonly ProfilerMarker _WriteChangesToCharacterFileMarker = new(nameof(_WriteChangesToCharacterFileMarker).NicifyVariable());
        private static readonly ProfilerMarker _WriteWorldDataToFileCheckForChangesMarker = new(nameof(_WriteWorldDataToFileCheckForChangesMarker).NicifyVariable());
        private static readonly ProfilerMarker _SynchronizeChannelDirectoryTaskMarker = new(nameof(_SynchronizeChannelDirectoryTaskMarker).NicifyVariable());
        private const int _MAX_ENTITIES_COUNT_IN_CHUNK = 128;
        private const int _WAIT_FOR_READ_CHANNEL_DIR_TIMEOUT_IN_MILLISECONDS = 5000;

        private readonly Dictionary<CurrentPersistenceChannelIndex, string> _channelDirectories = new();
        private CurrentPersistenceChannelIndex _suggestedFreeChannelIndex;

        private NativeHashMap<PersistenceInstanceId, TrackedQuantity> _gameTrackedQuantitiesChangeLookup;
        private EntityQuery _skinOptionsQuery;
        private EntityQuery _skinColorOptionsQuery;
        private PersistenceCachedData _persistenceCachedData;
        private CollectWorldPersistentDataJob _collectWorldPersistentDataJob;
        private ECBSynchronizationSystem _ECBSyncSystem;

        private NativeList<PersistenceInstanceId> _collectedPersistentInstanceIDs;
        private NativeList<TrackedQuantity> _collectedTrackedQuantities;
        private EntityQuery _playerQuery;
        private Task _readAllChannelDirTask;
        private EntityQuery _getChannelIndexQuery;
        private CurrentPersistenceChannelIndex _currentChannelIndex;
        private PersistenceFileAccess _worldDataFileAccess;
        private PersistenceFileAccess _characterDataFileAccess;

        public string WorldPersistenceFilePath {
            get;
            private set;
        }

        public string CharactersPersistenceFilePath {
            get;
            private set;
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            _getChannelIndexQuery = SystemAPI.QueryBuilder()
                .WithAll<CurrentPersistenceChannelIndex>()
                .WithOptions(EntityQueryOptions.IncludeSystems)
                .Build();
            _readAllChannelDirTask = GetAllExistingPersistenceDirectory().LogException();
            EntityManager.AddComponent<CurrentPersistenceChannelIndex>(SystemHandle);
            var entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp);
            ref var state = ref CheckedStateRef;
            _playerQuery = entityQueryBuilder
                .WithMovementRoot()
                .Build(ref state);

            _worldDataFileAccess = new(Allocator.Persistent);
            _characterDataFileAccess = new(Allocator.Persistent);

            _collectWorldPersistentDataJob.AssignHandles(ref state);
            _collectedPersistentInstanceIDs = new(_MAX_ENTITIES_COUNT_IN_CHUNK, Allocator.Persistent);
            _collectedTrackedQuantities = new(_MAX_ENTITIES_COUNT_IN_CHUNK, Allocator.Persistent);
            _gameTrackedQuantitiesChangeLookup = new(_MAX_ENTITIES_COUNT_IN_CHUNK, Allocator.Persistent);
            _collectWorldPersistentDataJob.AssetIdList = new(_MAX_ENTITIES_COUNT_IN_CHUNK, Allocator.Persistent);
            _collectWorldPersistentDataJob.PositionList = new(_MAX_ENTITIES_COUNT_IN_CHUNK, Allocator.Persistent);
            _collectWorldPersistentDataJob.RotationList = new(_MAX_ENTITIES_COUNT_IN_CHUNK, Allocator.Persistent);
            _persistenceCachedData = new PersistenceCachedData(Allocator.Persistent);

            _skinOptionsQuery = SystemAPI.QueryBuilder()
                .WithAll<PersistentSkinOptionApply>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                .Build();
            _skinOptionsQuery.SetChangedVersionFilter<PersistentSkinOptionApply>();

            _skinColorOptionsQuery = SystemAPI.QueryBuilder()
                .WithAll<PersistentSkinColorOptionApply>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                .Build();
            _skinColorOptionsQuery.SetChangedVersionFilter<PersistentSkinColorOptionApply>();

            RequireForUpdate<PrefabGlobalRegisterLookup>();
        }

        private async Task GetAllExistingPersistenceDirectory()
        {
            _channelDirectories.Clear();
            Directory.CreateDirectory(PersistenceRootFolderPath);
            string[] directories = Directory.GetDirectories(PersistenceRootFolderPath);
            _channelDirectories.EnsureCapacity(directories.Length);
            var allFileTextTasks = new List<Task<string>>(directories.Length);
            var allFileTextStreams = new List<(int DirIndexInArray, IDisposable Stream)>(directories.Length);
            var dirIndexWithoutIndexFile = new List<int>(directories.Length);
            try
            {
                for ( var index = 0; index < directories.Length; index++ )
                {
                    string directory = directories[index];
                    string indexFilePath = GetIndexFilePath(directory);
                    if ( !File.Exists(indexFilePath) )
                    {
                        dirIndexWithoutIndexFile.Add(index);
                        continue;
                    }

                    var stream = File.OpenText(indexFilePath);
                    var readToEndAsync = stream.ReadToEndAsync();
                    allFileTextTasks.Add(readToEndAsync);
                    allFileTextStreams.Add((index, stream));
                }
                await Task.WhenAll(allFileTextTasks);

                for ( var index = 0; index < allFileTextTasks.Count; index++ )
                {
                    var allFileTextStreamWithDirIndex = allFileTextStreams[index];
                    allFileTextStreamWithDirIndex.Stream.Dispose();
                    var dirIndexInArray = allFileTextStreamWithDirIndex.DirIndexInArray;
                    string directory = directories[dirIndexInArray];
                    string fileJson = allFileTextTasks[index].Result;
                    var fileJsonDeserialized = JsonSerialization.FromJson<PersistenceIndexFile>(fileJson);
                    _channelDirectories.Add(fileJsonDeserialized.ChannelIndex, directory);
                }
            }
            catch
            {
                //we have to dispose all the streams that we have opened before throwing an exception
                for ( var index = 0; index < allFileTextStreams.Count; index++ )
                {
                    allFileTextStreams[index].Stream?.Dispose();
                }
                LogHelper.LogErrorMessage($"failed to read all channel directories, a more explicit exception message should be logged.", "PersistenceSystem");
                throw;
            }

            _suggestedFreeChannelIndex = 0;
            for ( var i = 0; i < dirIndexWithoutIndexFile.Count; i++ )
            {
                int index = dirIndexWithoutIndexFile[i];
                string channelDirPath = directories[index];
                string indexFilePath = GetIndexFilePath(channelDirPath);
                index = ResolveChannelIndex(_suggestedFreeChannelIndex);
                _channelDirectories.Add(index, channelDirPath);
                WriteToPersistenceIndexFile(new PersistenceIndexFile(index), indexFilePath);
                _suggestedFreeChannelIndex = index + 1;
            }
        }

        private int ResolveChannelIndex(int index)
        {
            while ( _channelDirectories.ContainsKey(index) )
            {
                index++;
            }
            return index;
        }

        protected override unsafe void OnStartRunning()
        {
            base.OnStartRunning();
            _ECBSyncSystem = World.GetOrCreateSystemManaged<ECBSynchronizationSystem>();
            _currentChannelIndex = _getChannelIndexQuery.GetSingleton<CurrentPersistenceChannelIndex>();
            InitializePersistenceForChannel(_currentChannelIndex);
        }

        private unsafe void InitializePersistenceForChannel(CurrentPersistenceChannelIndex currentChannelIndex)
        {
            _persistenceCachedData.CharacterDataLookup.Clear();
            SynchronizeChannelDirectoryTask();

            if ( _channelDirectories.Count == 0 )
            {
                currentChannelIndex.Value = 0;
                CreateChannelDirectoryAtIndex(currentChannelIndex);
            }
            string channelDirectoryPath = _channelDirectories[currentChannelIndex];
            CharactersPersistenceFilePath = Path.Combine(channelDirectoryPath, ChatUsersDataFile);
            WorldPersistenceFilePath = Path.Combine(channelDirectoryPath, WorldDataFile);
            _characterDataFileAccess.InitializeStream(CharactersPersistenceFilePath);
            _worldDataFileAccess.InitializeStream(WorldPersistenceFilePath);

            DeserializeWorldDataFromFile();
            DeserializeAndCacheCharacterDataFromFile();
            EntityManager.AddComponentData(SystemHandle, _persistenceCachedData);
        }

        private unsafe void DeserializeAndCacheCharacterDataFromFile()
        {
            if ( !_characterDataFileAccess.TryGetBytesFromFile() )
                return;

            int streamBytesLength = _characterDataFileAccess.StreamBytes.Length;
            var deserializationContext = new DeserializationContext(_characterDataFileAccess.StreamBytes.GetUnsafePtr(), streamBytesLength);
            AllocatorManager.AllocatorHandle allocatorHandle = Allocator.Temp;
            DeserializeCharacterDataFromBytes(ref deserializationContext, ref allocatorHandle, out var characterFile);
            if ( deserializationContext.Position != streamBytesLength )
            {
                LogHelper.LogErrorMessage($"only {deserializationContext.Position} bytes have been read out of {streamBytesLength} present in the character serialization file :( {CharactersPersistenceFilePath} ).", "PersistenceSystem");
            }
            if ( characterFile.ChatUsers.Length > 0 )
            {
                _persistenceCachedData.CharacterDataLookup.AddRangeFromZero(in characterFile.ChatUsers, characterFile.Currencies, characterFile.SkinIndicesDatas, characterFile.ColorIndicesDatas);
            }
        }

        private unsafe void DeserializeWorldDataFromFile()
        {
            if ( !_worldDataFileAccess.TryGetBytesFromFile() )
                return;

            EntityManager entityManager = EntityManager;
            var prefabGlobalRegisterLookup = SystemAPI.GetSingleton<PrefabGlobalRegisterLookup>();
            int streamBytesLength = _worldDataFileAccess.StreamBytes.Length;
            var deserializationContext = new DeserializationContext(_worldDataFileAccess.StreamBytes.GetUnsafePtr(), streamBytesLength);

            DeserializeWorldDataFromBytes(in prefabGlobalRegisterLookup, ref entityManager, ref deserializationContext, ref _gameTrackedQuantitiesChangeLookup, out var playerTransform);
            if ( deserializationContext.Position != streamBytesLength )
            {
                LogHelper.LogErrorMessage($"only {deserializationContext.Position} bytes have been read out of {streamBytesLength} present in the world data serialization file :( {WorldPersistenceFilePath} ).", "PersistenceSystem");
            }
            EntityManager.AddComponentData(SystemHandle, new PlayerPersistentWorldData {
                Position = playerTransform.Position,
                Rotation = playerTransform.Rotation,
            });
        }

        private void SynchronizeChannelDirectoryTask()
        {
            switch ( _readAllChannelDirTask.Status )
            {
                //task is not finished yet, we have to wait for it to be done before the game state can continue
                case TaskStatus.Running:
                case TaskStatus.WaitingForActivation:
                case TaskStatus.WaitingForChildrenToComplete:

                    using ( _SynchronizeChannelDirectoryTaskMarker.Auto() )
                    {
                        _readAllChannelDirTask.Wait(_WAIT_FOR_READ_CHANNEL_DIR_TIMEOUT_IN_MILLISECONDS);
                    }
                    break;

                //the task has completed nothing to do here
                case TaskStatus.RanToCompletion:
                    break;

                //any other case should be an error as the app persistence depend on this task to be working 
                default:
                    LogHelper.LogErrorMessage($"failed to read all channel directories", "PersistenceSystem");
                    break;
            }
            _readAllChannelDirTask = Task.CompletedTask;
        }

        private CurrentPersistenceChannelIndex CreateNewPersistenceChannelDirectory()
        {
            _suggestedFreeChannelIndex = ResolveChannelIndex(_suggestedFreeChannelIndex);
            CreateChannelDirectoryAtIndex(_suggestedFreeChannelIndex);
            var currentChannelIndex = _suggestedFreeChannelIndex;
            _suggestedFreeChannelIndex.Value++;
            return currentChannelIndex;
        }

        private void CreateChannelDirectoryAtIndex(CurrentPersistenceChannelIndex channelIndex)
        {
            var channelDirPath = GetChannelDirPath(channelIndex);
            Directory.CreateDirectory(channelDirPath);
            _channelDirectories.Add(channelIndex, channelDirPath);
            WriteToPersistenceIndexFile(new PersistenceIndexFile(channelIndex), GetIndexFilePath(channelDirPath));
        }

        private static void PreAllocateFileBuffers(ReadOnlySpan<int> bufferSizes)
        {
            var buffers = new List<byte[]>(bufferSizes.Length);
            for ( var index = 0; index < bufferSizes.Length; index++ )
            {
                int bufferSize = bufferSizes[index];
                buffers.Add(ArrayPool<byte>.Shared.Rent(bufferSize));
            }
            foreach ( var buffer in buffers )
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static void WriteToPersistenceIndexFile(PersistenceIndexFile persistenceIndexFile, string indexFilePath)
        {
            var fileJson = JsonSerialization.ToJson(persistenceIndexFile);
            File.WriteAllText(indexFilePath, fileJson);
        }

        private static string GetIndexFilePath(string channelDirPath)
        {
            return Path.Combine(channelDirPath, PersistenceIndexFileName);
        }

        [GeneratePropertyBag]
        [Serializable]
        public struct PersistenceIndexFile
        {
            public CurrentPersistenceChannelIndex ChannelIndex;
            public string ChannelDirectoryDisplayName;

            public PersistenceIndexFile(CurrentPersistenceChannelIndex channelIndex)
            {
                ChannelIndex = channelIndex;
                ChannelDirectoryDisplayName = $"World {(channelIndex + 1).ToString()}";
            }
        }

        [BurstCompile]
        public unsafe struct SerializeWorldDataToBinaryJob : IJob
        {
            private static readonly ProfilerMarker _ExecuteMarker = new("SerializeWorldDataToBinaryJobMarker");

            public SerializationContext SerializerContext;
            public LocalTransform PlayerTransform;
            [ReadOnly] public NativeList<PrefabAssetID> PrefabAssetIds;
            [ReadOnly] public NativeList<float3> PositionList;
            [ReadOnly] public NativeList<quaternion> RotationList;
            [ReadOnly] public NativeList<PersistenceInstanceId> PersistentInstanceIDs;
            [ReadOnly] public NativeList<TrackedQuantity> TrackedQuantities;

            public void Execute()
            {
                _ExecuteMarker.Begin();
                SerializerContext.Write(new FileHeader { SerializationVersion = WorldFileHeader.LATEST_SERIALIZATION_VERSION });
                SerializerContext.Write(new WorldFileHeader.V1 {
                    PlayerPosition = PlayerTransform.Position,
                    PlayerRotation = PlayerTransform.Rotation,
                    EntityLength = PrefabAssetIds.Length,
                });
                SerializerContext.Write(PrefabAssetIds.GetUnsafeReadOnlyPtr(), PrefabAssetIds.Length);
                SerializerContext.Write(PositionList.GetUnsafeReadOnlyPtr(), PositionList.Length);
                SerializerContext.Write(RotationList.GetUnsafeReadOnlyPtr(), RotationList.Length);
                SerializerContext.WriteAsArray(PersistentInstanceIDs.GetUnsafeReadOnlyPtr(), PersistentInstanceIDs.Length);
                SerializerContext.Write(TrackedQuantities.GetUnsafeReadOnlyPtr(), TrackedQuantities.Length);
                _ExecuteMarker.End();
            }
        }

        [BurstCompile]
        private static unsafe void DeserializeWorldDataFromBytes(in PrefabGlobalRegisterLookup prefabGlobalRegisterLookup, ref EntityManager entityManager, ref DeserializationContext deserializationContext, ref NativeHashMap<PersistenceInstanceId, TrackedQuantity> trackedQuantitiesChangeLookup, out LocalTransform playerTransform)
        {
            var fileHeader = deserializationContext.ReadNext<FileHeader>();
            playerTransform = default;
            playerTransform.Rotation = quaternion.identity;
            switch ( fileHeader.SerializationVersion )
            {
                case >= 1:
                {
                    var worldFileHeader = deserializationContext.ReadNext<WorldFileHeader.V1>();
                    playerTransform.Position = worldFileHeader.PlayerPosition;
                    playerTransform.Rotation = worldFileHeader.PlayerRotation;

                    if ( worldFileHeader.EntityLength > 0 )
                    {
                        var prefabDatas = new NativeArray<PrefabAssetID>(worldFileHeader.EntityLength, Allocator.TempJob);
                        var positionData = new NativeArray<float3>(worldFileHeader.EntityLength, Allocator.TempJob);
                        var rotationData = new NativeArray<quaternion>(worldFileHeader.EntityLength, Allocator.TempJob);
                        //load world data from disk bytes
                        DeserializeTo(ref deserializationContext, ref prefabDatas, worldFileHeader.EntityLength);
                        DeserializeTo(ref deserializationContext, ref positionData, worldFileHeader.EntityLength);
                        DeserializeTo(ref deserializationContext, ref rotationData, worldFileHeader.EntityLength);
                        // instantiate world objects
                        for ( var index = 0; index < prefabDatas.Length; index++ )
                        {
                            var prefabAssetID = prefabDatas[index];
                            var entity = entityManager.Instantiate(prefabGlobalRegisterLookup.Value[prefabAssetID]);
                            ref var localTransform = ref entityManager.GetComponentDataRW<LocalTransform>(entity).ValueRW;
                            localTransform.Position = positionData[index];
                            localTransform.Rotation = rotationData[index];
                        }

                        prefabDatas.Dispose();
                        positionData.Dispose();
                        rotationData.Dispose();
                    }

                    if ( deserializationContext.Position >= deserializationContext.Length )
                        return; //nothing more to deserialize

                    var instanceIdsLength = deserializationContext.ReadNext<int>();
                    var persistenceInstanceIds = (PersistenceInstanceId*)deserializationContext.ReadNext<PersistenceInstanceId>(instanceIdsLength);
                    var trackedQuantities = (TrackedQuantity*)deserializationContext.ReadNext<TrackedQuantity>(instanceIdsLength);
                    trackedQuantitiesChangeLookup.Clear();
                    for ( int i = 0; i < instanceIdsLength; i++ )
                    {
                        trackedQuantitiesChangeLookup.Add(persistenceInstanceIds[i], trackedQuantities[i]);
                    }
                    break;
                }
                default:
                    Debug.LogError($"unknow serialization version: {fileHeader.SerializationVersion}");
                    break;
            }
        }

        private static unsafe void DeserializeTo<T>(ref DeserializationContext deserializationContext, ref NativeArray<T> prefabData, int length)
            where T : unmanaged
        {
            int collectionSizeInBytes = sizeof(T) * length;
            UnsafeUtility.MemCpy(prefabData.GetUnsafePtr(), deserializationContext.ReadNext(collectionSizeInBytes), collectionSizeInBytes);
        }

        private static unsafe T* CreateNativeArrayAndGetPtr<T>(AllocatorManager.AllocatorHandle allocator, int chatUsersLength, out NativeArray<T> skinColorOptionsEnumerable, NativeArrayOptions nativeArrayOptions = NativeArrayOptions.UninitializedMemory)
            where T : unmanaged
        {
            skinColorOptionsEnumerable = CollectionHelper.CreateNativeArray<T>(chatUsersLength, allocator, nativeArrayOptions);
            return GetUnsafePtrAsT(skinColorOptionsEnumerable);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe T* GetUnsafePtrAsT<T>(NativeArray<T> nativeArray)
            where T : unmanaged
        {
            return (T*)nativeArray.GetUnsafePtr();
        }


        private string GetCurrentChannelDirPath()
        {
            return GetChannelDirPath(EntityManager.GetComponentData<CurrentPersistenceChannelIndex>(SystemHandle).Value);
        }

        private string GetChannelDirPath(CurrentPersistenceChannelIndex currentChannelIndex)
        {
            return string.Format(ChannelFolderPathFormat, currentChannelIndex.ToString());
        }

        protected override void OnUpdate()
        {
            var characterDataJobs = WriteChangesToCharacterFile();
            var worldDataToFileJobs = WriteWorldDataToFile();
            if ( !characterDataJobs.Equals(worldDataToFileJobs) )
            {
                Dependency = JobHandle.CombineDependencies(characterDataJobs, worldDataToFileJobs);
            }
            _ECBSyncSystem.AddJobHandleForProducer(Dependency);
        }

        private JobHandle WriteChangesToCharacterFile()
        {
            using ( _WriteChangesToCharacterFileMarker.Auto() )
            {
                _characterDataFileAccess.WriteBinariesToFileWithTimeout(World.Time.ElapsedTime); //write bytes From Last Frame if any
                var streamBytes = _characterDataFileAccess.StreamBytes;
                var persistenceCharacterDataQuery = SystemAPI.QueryBuilder()
                    .WithAll<CharacterHierarchyHubData, GameCurrency, ChatUserComponent>()
                    .Build();
                persistenceCharacterDataQuery.SetChangedVersionFilter<GameCurrency>();

                if ( persistenceCharacterDataQuery.IsEmpty
                     && _skinColorOptionsQuery.IsEmpty
                     && _skinOptionsQuery.IsEmpty )
                    return GetDefaultJobHandle();
                streamBytes.Clear();
                persistenceCharacterDataQuery.ResetFilter();
                JobHandle resultDeps = default;
                resultDeps = new CollectPersistenceCharacterDataFromEntitiesJob {
                    CachedDataCachedData = _persistenceCachedData,
                    CharacterSkinApplyLookup = SystemAPI.GetBufferLookup<PersistentSkinOptionApply>(true),
                    CharacterColorApplyLookup = SystemAPI.GetBufferLookup<PersistentSkinColorOptionApply>(true)
                }.Schedule(persistenceCharacterDataQuery, Dependency);

                resultDeps = new ConstructAllCharacterDataBinariesJob {
                    StreamBytes = streamBytes,
                    PersistenceCachedRef = _persistenceCachedData
                }.Schedule(resultDeps);

                return resultDeps;
            }
        }

        [BurstCompile]
        public partial struct ConstructAllCharacterDataBinariesJob : IJob
        {
            private static readonly ProfilerMarker _ExecuteMarker = new(nameof(ConstructAllCharacterDataBinariesJob) + "Marker");
            public NativeList<byte> StreamBytes;
            public PersistenceCachedData PersistenceCachedRef;

            public void Execute()
            {
                _ExecuteMarker.Begin();
                ConstructAllCharacterBytesFile(ref StreamBytes, ref PersistenceCachedRef);
                _ExecuteMarker.End();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void ConstructAllCharacterBytesFile(ref NativeList<byte> streamBytes, ref PersistenceCachedData persistenceCachedData)
        {
            streamBytes.Clear();
            var chatUsers = persistenceCachedData.CharacterDataLookup.AsKeysArray();
            persistenceCachedData.CharacterDataLookup.AsValuesArray(out var characterCurrencies, out var characterSkinIndicesDatas, out var characterColorIndicesDatas);

            var serializerContext = new SerializationContext(streamBytes);
            serializerContext.Write(new FileHeader { SerializationVersion = CharacterFileHeader.LATEST_SERIALIZATION_VERSION });
            serializerContext.Write(new CharacterFileHeader.V1 { ChatUserLength = persistenceCachedData.CharacterDataLookup.Length });
            new ChatUserBinaryAdapter().Serialize(ref serializerContext, chatUsers);
            default(GameCurrency.BinaryAdapter).Serialize(ref serializerContext, characterCurrencies);
            default(SkinOptions.BinaryAdapter).Serialize(ref serializerContext, characterSkinIndicesDatas);
            default(SkinColorOptions.BinaryAdapter).Serialize(ref serializerContext, characterColorIndicesDatas);
        }

        [BurstCompile]
        internal static unsafe void DeserializeCharacterDataFromBytes(ref DeserializationContext deserializationContext, ref AllocatorManager.AllocatorHandle allocator, out SerializedCharacterDatasFile serializedCharacterData)
        {
            serializedCharacterData = default;
            if ( deserializationContext.IsEmpty )
                return;

            var serializationVersion = deserializationContext.ReadNext<FileHeader>().SerializationVersion;
            switch ( serializationVersion )
            {
                case 1:
                {
                    var header = deserializationContext.ReadNext<CharacterFileHeader.V1>();
                    var chatUsersLength = header.ChatUserLength;
                    default(ChatUserBinaryAdapter).Deserialize(ref deserializationContext, CreateNativeArrayAndGetPtr(allocator, chatUsersLength, out serializedCharacterData.ChatUsers), chatUsersLength);
                    default(GameCurrency.BinaryAdapter).Deserialize(ref deserializationContext, CreateNativeArrayAndGetPtr(allocator, chatUsersLength, out serializedCharacterData.Currencies), chatUsersLength);
                    default(SkinOptions.BinaryAdapter).Deserialize(ref deserializationContext, CreateNativeArrayAndGetPtr(allocator, chatUsersLength, out serializedCharacterData.SkinIndicesDatas), chatUsersLength);
                    default(SkinColorOptions.BinaryAdapter).Deserialize(ref deserializationContext, CreateNativeArrayAndGetPtr(allocator, chatUsersLength, out serializedCharacterData.ColorIndicesDatas), chatUsersLength);
                    break;
                }
                default:
                {
                    Debug.LogError($"unknow serialization version: {serializationVersion}");
                    break;
                }
            }
        }

        internal struct FileHeader
        {
            public int SerializationVersion;
        }

        [GeneratePropertyBag]
        internal struct CharacterFileHeader
        {
            public const int LATEST_SERIALIZATION_VERSION = 1;

            internal struct V1
            {
                public int ChatUserLength;
            }
        }

        [BurstCompile]
        private partial struct CollectPersistenceCharacterDataFromEntitiesJob : IJobEntity
        {
            private static readonly ProfilerMarker _ExecuteMarker = new(nameof(CollectPersistenceCharacterDataFromEntitiesJob) + "Marker");

            [NativeDisableContainerSafetyRestriction]
            public PersistenceCachedData CachedDataCachedData;
            [ReadOnly] public BufferLookup<PersistentSkinOptionApply> CharacterSkinApplyLookup;
            [ReadOnly] public BufferLookup<PersistentSkinColorOptionApply> CharacterColorApplyLookup;

            void Execute(in ChatUserComponent chatUserComponent, in CharacterHierarchyHubData characterHierarchyHubData, in GameCurrency gameCurrency)
            {
                _ExecuteMarker.Begin();
                var chatUserPropertiesPersistence = new ChatUserPropertiesPersistence {
                    Currency = gameCurrency,
                };
                var persistentSkinOptionApplies = CharacterSkinApplyLookup[characterHierarchyHubData.AnimationRoot];
                var persistentSkinColorOptionApplies = CharacterColorApplyLookup[characterHierarchyHubData.AnimationRoot];
                WriteCharacterPersistentData(ref CachedDataCachedData, chatUserComponent.UserId, persistentSkinOptionApplies.AsNativeArray(), persistentSkinColorOptionApplies.AsNativeArray(), ref chatUserPropertiesPersistence);
                _ExecuteMarker.End();
            }
        }

        public static unsafe void WriteCharacterPersistentData(ref PersistenceCachedData persistenceCachedData, in ChatUser chatUser, NativeArray<PersistentSkinOptionApply> characterSwapSkinApplies, NativeArray<PersistentSkinColorOptionApply> characterColorOptionApplies, ref ChatUserPropertiesPersistence characterData)
        {
            characterData.SkinOptions.Indices.AddRange(characterSwapSkinApplies.GetUnsafeReadOnlyPtr(), characterSwapSkinApplies.Length);
            characterData.SkinColorOptions.Indices.AddRange(characterColorOptionApplies.GetUnsafeReadOnlyPtr(), characterColorOptionApplies.Length);
            persistenceCachedData.CharacterDataLookup.AddOrReplace(in chatUser, characterData.Currency, characterData.SkinOptions, characterData.SkinColorOptions);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private JobHandle GetDefaultJobHandle() => Dependency;

        private JobHandle WriteWorldDataToFile()
        {
            _collectWorldPersistentDataJob.UpdateHandles(ref CheckedStateRef);
            _worldDataFileAccess.WriteBinariesToFileWithTimeout(World.Time.ElapsedTime); //write bytes From Last Frame if any
            var streamBytes = _worldDataFileAccess.StreamBytes;

            bool hasPlayer = !_playerQuery.IsEmptyIgnoreFilter;
            bool hasChangeSimulate;
            bool hasChangeTransform;
            bool hasStructuralChanges;
            bool hasChangedTrackedQuantity;
            var allWorldPersistableQuery = SystemAPI.QueryBuilder()
                .WithAll<Simulate, LocalTransform, PrefabAssetID>()
                .Build();
            var trackedQuantityQuery = SystemAPI.QueryBuilder().WithAll<TrackedQuantity, PersistenceInstanceId>().Build();
            using ( _WriteWorldDataToFileCheckForChangesMarker.Auto() )
            {
                trackedQuantityQuery.SetChangedVersionFilter<TrackedQuantity>();
                hasChangedTrackedQuantity = !trackedQuantityQuery.IsEmpty;
                trackedQuantityQuery.ResetFilter();
                allWorldPersistableQuery.SetChangedVersionFilter<Simulate>();
                hasChangeSimulate = !allWorldPersistableQuery.IsEmpty;
                allWorldPersistableQuery.SetChangedVersionFilter<LocalTransform>();
                hasChangeTransform = !allWorldPersistableQuery.IsEmpty;
                allWorldPersistableQuery.SetOrderVersionFilter();
                hasStructuralChanges = !allWorldPersistableQuery.IsEmpty;
            }
            var applyTrackedChangeFromPersistenceJob = Dependency;
            // there is new trackedQuantities and we have some from persistence to apply
            if ( hasChangedTrackedQuantity && _gameTrackedQuantitiesChangeLookup.Count > 0 )
            {
                applyTrackedChangeFromPersistenceJob = new ApplyTrackedQuantityPersistedChangesJob {
                    GameTrackedQuantitiesChangeLookup = _gameTrackedQuantitiesChangeLookup
                }.Schedule(trackedQuantityQuery, Dependency);
            }
            if ( !hasPlayer || !hasStructuralChanges && !hasChangeTransform && !hasChangeSimulate && !hasChangedTrackedQuantity )
                return applyTrackedChangeFromPersistenceJob;

            streamBytes.Clear();
            using ( _WriteWorldDataToFileScheduleCollectJobsMarker.Auto() )
            {
                allWorldPersistableQuery.ResetFilter();
                _collectedPersistentInstanceIDs.Clear();
                _collectedTrackedQuantities.Clear();
                var collectTrackedQuantityJob = new CollectTrackedQuantityJob {
                    PersistentAssetIDHandle = SystemAPI.GetComponentTypeHandle<PersistenceInstanceId>(true),
                    TrackedQuantityHandle = SystemAPI.GetComponentTypeHandle<TrackedQuantity>(true),
                    PersistentAssetIDs = _collectedPersistentInstanceIDs,
                    TrackedQuantities = _collectedTrackedQuantities,
                };
                var serializeWorldDataToBinaryJob = new SerializeWorldDataToBinaryJob {
                    SerializerContext = new SerializationContext(streamBytes),
                    PlayerTransform = _playerQuery.GetSingleton<LocalTransform>(),
                    PrefabAssetIds = _collectWorldPersistentDataJob.AssetIdList,
                    RotationList = _collectWorldPersistentDataJob.RotationList,
                    PositionList = _collectWorldPersistentDataJob.PositionList,
                    PersistentInstanceIDs = _collectedPersistentInstanceIDs,
                    TrackedQuantities = _collectedTrackedQuantities,
                };

                //we use applyTrackedChangeFromPersistenceJob as a dependency to make sure we are not saving outdated data
                //which has not been rectified from persistence yet
                JobHandle trackedQuantitiesJob = collectTrackedQuantityJob.Schedule(trackedQuantityQuery, applyTrackedChangeFromPersistenceJob);
                JobHandle resultHandle = _collectWorldPersistentDataJob.Schedule(allWorldPersistableQuery, Dependency);
                resultHandle = JobHandle.CombineDependencies(resultHandle, trackedQuantitiesJob);
                resultHandle = serializeWorldDataToBinaryJob.Schedule(resultHandle);
                return resultHandle;
            }
        }

        /// <summary>
        /// enableable components are not supported by this job
        /// </summary>
        [BurstCompile]
        private unsafe struct CollectTrackedQuantityJob : IJobChunk
        {
            private static readonly ProfilerMarker _ExecuteChunkMarker = new("CollectTrackedQuantityJobMarker");

            [ReadOnly] public ComponentTypeHandle<PersistenceInstanceId> PersistentAssetIDHandle;
            [ReadOnly] public ComponentTypeHandle<TrackedQuantity> TrackedQuantityHandle;
            [WriteOnly] public NativeList<PersistenceInstanceId> PersistentAssetIDs;
            [WriteOnly] public NativeList<TrackedQuantity> TrackedQuantities;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                _ExecuteChunkMarker.Begin();
                var entityChunkCount = chunk.Count;
                PersistentAssetIDs.AddRange(chunk.GetRequiredComponentDataPtrROAsT(ref PersistentAssetIDHandle), entityChunkCount);
                TrackedQuantities.AddRange(chunk.GetRequiredComponentDataPtrROAsT(ref TrackedQuantityHandle), entityChunkCount);
                _ExecuteChunkMarker.End();
            }
        }

        [BurstCompile]
        public partial struct ApplyTrackedQuantityPersistedChangesJob : IJobEntity
        {
            private static readonly ProfilerMarker _ExecuteMarker = new(nameof(ApplyTrackedQuantityPersistedChangesJob) + "Marker");
            public NativeHashMap<PersistenceInstanceId, TrackedQuantity> GameTrackedQuantitiesChangeLookup;

            public void Execute(in PersistenceInstanceId instanceId, ref TrackedQuantity trackedQuantity)
            {
                _ExecuteMarker.Begin();
                if ( GameTrackedQuantitiesChangeLookup.TryGetValue(instanceId, out var trackedQuantityChange) )
                {
                    trackedQuantity = trackedQuantityChange;
                    GameTrackedQuantitiesChangeLookup.Remove(instanceId);
                }
                _ExecuteMarker.End();
            }
        }

        [BurstCompile]
        public unsafe struct CollectWorldPersistentDataJob : IJobChunk
        {
            private static readonly ProfilerMarker ExecuteMarker = new("CollectWorldPersistentDataJobMarker");

            public NativeList<PrefabAssetID> AssetIdList;
            public NativeList<float3> PositionList;
            public NativeList<quaternion> RotationList;

            [ReadOnly] private ComponentTypeHandle<LocalTransform> _transformHandle;
            [ReadOnly] private ComponentTypeHandle<PrefabAssetID> _assetIdHandle;


            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AssignHandles(ref SystemState state)
            {
                state.GetComponentTypeHandle(out _transformHandle, true);
                state.GetComponentTypeHandle(out _assetIdHandle, true);
            }

            public void UpdateHandles(ref SystemState state)
            {
                AssetIdList.Clear();
                PositionList.Clear();
                RotationList.Clear();
                _assetIdHandle.Update(ref state);
                _transformHandle.Update(ref state);
            }

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                ExecuteMarker.Begin();
                var chunkEntityCount = chunk.Count;
                var transforms = chunk.GetRequiredComponentDataPtrROAsT(ref _transformHandle);
                var assetIds = chunk.GetRequiredComponentDataPtrROAsT(ref _assetIdHandle);

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void ExecuteOnIndex(int entityIndexInChunk, in CollectWorldPersistentDataJob job)
                {
                    var transform = transforms[entityIndexInChunk];
                    job.PositionList.Add(transform.Position);
                    job.RotationList.Add(transform.Rotation);
                }

                if ( !useEnabledMask )
                {
                    AddRangePrefabIds(assetIds, chunk.Count);
                    for ( int entityIndexInChunk = 0; entityIndexInChunk < chunkEntityCount; ++entityIndexInChunk )
                    {
                        ExecuteOnIndex(entityIndexInChunk, in this);
                    }
                }
                else
                {
                    int edgeCount = math.countbits(chunkEnabledMask.ULong0 ^ (chunkEnabledMask.ULong0 << 1)) + math.countbits(chunkEnabledMask.ULong1 ^ (chunkEnabledMask.ULong1 << 1)) - 1;
                    bool useRanges = edgeCount <= 4;
                    if ( useRanges )
                    {
                        int chunkEndIndex = 0;

                        while ( InternalCompilerInterface.UnsafeTryGetNextEnabledBitRange(chunkEnabledMask, chunkEndIndex, out var entityIndexInChunk, out chunkEndIndex) )
                        {
                            AddRangePrefabIds((assetIds + entityIndexInChunk), (chunkEndIndex - entityIndexInChunk));
                            while ( entityIndexInChunk < chunkEndIndex )
                            {
                                ExecuteOnIndex(entityIndexInChunk, in this);
                                entityIndexInChunk++;
                            }
                        }
                    }
                    else
                    {
                        ulong mask64 = chunkEnabledMask.ULong0;
                        int count = math.min(64, chunkEntityCount);
                        for ( int entityIndexInChunk = 0; entityIndexInChunk < count; ++entityIndexInChunk )
                        {
                            if ( (mask64 & 1) != 0 )
                            {
                                AddSingleAssetID(assetIds[entityIndexInChunk]);
                                ExecuteOnIndex(entityIndexInChunk, in this);
                            }
                            mask64 >>= 1;
                        }
                        mask64 = chunkEnabledMask.ULong1;
                        for ( int entityIndexInChunk = 64; entityIndexInChunk < chunkEntityCount; ++entityIndexInChunk )
                        {
                            if ( (mask64 & 1) != 0 )
                            {
                                AddSingleAssetID(assetIds[entityIndexInChunk]);
                                ExecuteOnIndex(entityIndexInChunk, in this);
                            }
                            mask64 >>= 1;
                        }
                    }
                }
                ExecuteMarker.End();
            }

            private void AddRangePrefabIds(PrefabAssetID* assetIds, int entityCount)
            {
                AssetIdList.AddRange(assetIds, entityCount);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void AddSingleAssetID(in PrefabAssetID prefabAssetID)
            {
                AssetIdList.Add(in prefabAssetID);
            }

            public void Dispose()
            {
                AssetIdList.Dispose();
                PositionList.Dispose();
                RotationList.Dispose();
            }

            public void Dispose(JobHandle deps)
            {
                AssetIdList.Dispose(deps);
                PositionList.Dispose(deps);
                RotationList.Dispose(deps);
            }
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            WriteChangesAndFreeStreams();
        }

        private void WriteChangesAndFreeStreams()
        {
            _ = _worldDataFileAccess.WriteBinaryToFileAndFreeStream();
            _ = _characterDataFileAccess.WriteBinaryToFileAndFreeStream();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Dependency.Complete();
            _persistenceCachedData.CharacterDataLookup.Dispose();
            _worldDataFileAccess.Dispose();
            _characterDataFileAccess.Dispose();
            _gameTrackedQuantitiesChangeLookup.Dispose();
            _collectedPersistentInstanceIDs.Dispose();
            _collectedTrackedQuantities.Dispose();
            _collectWorldPersistentDataJob.Dispose();
        }


        public struct PersistenceCachedData : IComponentData
        {
            public MultiNativeFastReadLookup<ChatUser, GameCurrency, SkinOptions, SkinColorOptions> CharacterDataLookup;

            public PersistenceCachedData(Allocator allocator, int initialCharacterCapacity = 100)
            {
                CharacterDataLookup = new(initialCharacterCapacity, allocator);
            }
        }

        public struct PlayerPersistentWorldData : IComponentData
        {
            public float3 Position;
            public quaternion Rotation;
        }

        [Serializable]
        [GeneratePropertyBag]
        public struct CurrentPersistenceChannelIndex : IComponentData, IEquatable<CurrentPersistenceChannelIndex>
        {
            public short Value;

            public override string ToString()
            {
                return Value.ToString();
            }

            public bool Equals(CurrentPersistenceChannelIndex other)
            {
                return Value == other.Value;
            }

            public override bool Equals(object obj)
            {
                return obj is CurrentPersistenceChannelIndex other && Equals(other);
            }

            public override int GetHashCode()
            {
                return Value.GetHashCode();
            }

            public static bool operator ==(CurrentPersistenceChannelIndex left, CurrentPersistenceChannelIndex right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(CurrentPersistenceChannelIndex left, CurrentPersistenceChannelIndex right)
            {
                return !left.Equals(right);
            }

            public static implicit operator short(CurrentPersistenceChannelIndex value) => value.Value;

            public static implicit operator CurrentPersistenceChannelIndex(int value) => new() {
                Value = (short)value
            };

            public static implicit operator CurrentPersistenceChannelIndex(short value) => new() {
                Value = value
            };
        }

        internal struct SerializedCharacterDatasFile
        {
            #region SerializationVersion1Only
            public float3 PlayerPosition;
            public quaternion PlayerRotation;
            #endregion
            public NativeArray<ChatUser> ChatUsers;
            public NativeArray<GameCurrency> Currencies;
            public NativeArray<SkinOptions> SkinIndicesDatas;
            public NativeArray<SkinColorOptions> ColorIndicesDatas;
        }

        private struct WorldFileHeader
        {
            public const int LATEST_SERIALIZATION_VERSION = 1;

            public struct V1
            {
                public float3 PlayerPosition;
                public quaternion PlayerRotation;
                public int EntityLength;
            }
        }

        internal class PersistenceFileAccess : IDisposable
        {
            private const float DISK_SAVE_TIME_OUT = 5f;

            private readonly double _diskSaveTimeOut;
            private double _lastUpdateTime;
            private FileStream _fileStream;
            private Task _previousWriteTask;
            public NativeList<byte> StreamBytes;

            public PersistenceFileAccess(Allocator allocator, int initialBytesCapacity = 64, double diskSaveTimeOut = DISK_SAVE_TIME_OUT)
            {
                StreamBytes = new(initialBytesCapacity, allocator);
                _diskSaveTimeOut = diskSaveTimeOut;
                _previousWriteTask = Task.CompletedTask;
            }

            /// <summary>
            /// will be able to update right away 
            /// </summary>
            /// <param name="fileFullPath"></param>
            public void InitializeStream(string fileFullPath)
            {
                InitializeStream(fileFullPath, -_diskSaveTimeOut);
            }

            public void InitializeStream(string fileFullPath, double lastUpdateTime)
            {
                if ( _fileStream != null )
                {
                    try
                    {
                        _fileStream.Dispose();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e);
                    }
                }
                _fileStream = new FileStream(fileFullPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                _lastUpdateTime = lastUpdateTime;
            }

            public async Task WriteBinaryToFileAndFreeStream()
            {
                WriteBinaryToFile();
                await _previousWriteTask;
                FreeStream();
            }

            public void WriteBinariesToFileWithTimeout(double timeElapsedTime)
            {
                if ( timeElapsedTime < _lastUpdateTime + _diskSaveTimeOut )
                    return;

                if ( WriteBinaryToFile() )
                    _lastUpdateTime = timeElapsedTime;
            }

            private bool WriteBinaryToFile()
            {
                if ( StreamBytes.IsEmpty || !_previousWriteTask.IsCompleted )
                    return false;

                _previousWriteTask = WriteBinariesToFileAsync(StreamBytes.AsArray(), _fileStream).LogException();
                StreamBytes.Clear();
                return true;
            }

            public unsafe bool TryGetBytesFromFile()
            {
                var fileStreamLength = (int)_fileStream.Length;
                var dataBytes = ArrayPool<byte>.Shared.Rent((int)fileStreamLength);
                fileStreamLength = _fileStream.Read(dataBytes, 0, fileStreamLength);
                fixed ( byte* ptr = dataBytes )
                {
                    StreamBytes.Clear();
                    StreamBytes.AddRange(ptr, fileStreamLength);
                }
                ArrayPool<byte>.Shared.Return(dataBytes);
                return fileStreamLength != 0;
            }

            public void FreeStream()
            {
                _fileStream?.Dispose();
                _fileStream = null;
            }

            public void Dispose()
            {
                StreamBytes.Dispose();
            }

            private static async Task WriteBinariesToFileAsync(NativeArray<byte> nativeStream, FileStream fileStream)
            {
                int fileStreamLength = nativeStream.Length;
                var bufferManagedArray = ArrayPool<byte>.Shared.Rent(fileStreamLength);
                try
                {
                    NativeArray<byte>.Copy(nativeStream, bufferManagedArray, fileStreamLength);
                    fileStream.Position = 0;
                    fileStream.SetLength(fileStreamLength);
                    await fileStream.WriteAsync(bufferManagedArray, 0, fileStreamLength);
                    fileStream.Flush(true);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(bufferManagedArray);
                }
            }
        }
    }

}