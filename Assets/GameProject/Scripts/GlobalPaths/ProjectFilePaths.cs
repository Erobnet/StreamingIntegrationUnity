using System.IO;

public class ProjectFilePaths
{
    public const string USERSETTINGS_DIRECTORY_NAME = "UserSettings";
    private static string _settingsDirectory;
    public static string SettingsDirectory => _settingsDirectory ??= Path.Combine(System.Environment.CurrentDirectory, USERSETTINGS_DIRECTORY_NAME);
#if UNITY_EDITOR
    public const string EDITORS_GENERATED_ASSET_FOLDER_PATH = "Assets/EditorGenerated/";
    public const string EDITORS_UNITYOBJECT_PROXY_FOLDER_PATH = EDITORS_GENERATED_ASSET_FOLDER_PATH + "ObjectRefs/";
#endif
    public static void EnsureFolderIsCreated(string folderPath)
    {
        Directory.CreateDirectory(folderPath);
    }

}