using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameProject.Persistence
{
    public static class PersistenceHelper
    {
#if UNITY_EDITOR
        [MenuItem(GameProjectHelper.GAME_PROJECT_ASSET_MENU + "Persistence/Delete Build Player Game Persistence Data")]
#endif
        private static void DeleteBuildPlayerPersistenceData()
        {
            DeleteDirectoryAtPath(PersistenceSystemPaths.BuildPlayerPersistenceRootFolderPath);
        }

#if UNITY_EDITOR
        [MenuItem(GameProjectHelper.GAME_PROJECT_ASSET_MENU + "Persistence/Delete Editor Game Persistence Data")]
#endif
        public static void DeletePersistenceData()
        {
            DeleteDirectoryAtPath(PersistenceSystemPaths.PersistenceRootFolderPath);
        }

        private static void DeleteDirectoryAtPath(string persistenceRootFolderPath)
        {
            if ( Directory.Exists(persistenceRootFolderPath) )
            {
                Directory.Delete(persistenceRootFolderPath, true);
            }
            LogHelper.LogDebugMessage($"Persistence data deleted at path : {persistenceRootFolderPath}");
        }
    }
}