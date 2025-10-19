using System;
using UnityEngine;

namespace GameProject.Persistence
{
    public static class PersistenceSystemPaths
    {
        internal static readonly string  BuildPlayerPersistenceRootFolderPath=  $"{Application.persistentDataPath}/{_PERSISTENCE_FOLDER_NAME}/";
        public static readonly string PersistenceRootFolderPath =
#if UNITY_EDITOR
            $"{ProjectFilePaths.SettingsDirectory}/{_PERSISTENCE_FOLDER_NAME}/"
#else
           BuildPlayerPersistenceRootFolderPath
#endif
            ;
        public static readonly string ChatUsersDataFile = $"db.data";
        public static readonly string WorldDataFile = $"world.data";
        public static readonly string ChannelFolderPathFormat = $"{PersistenceRootFolderPath}{{0}}/";
        public static readonly string PersistenceIndexFileName = $"index.json";
        private const string _PERSISTENCE_FOLDER_NAME = "Data";
    }
}