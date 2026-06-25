using System.IO;
using GameCult.Aetheria.State.Verse;
using UnityEngine;

public static class AetheriaUnityRuntimePaths
{
    private static DirectoryInfo _gameDataDirectory;
    private static string _runtimeStateFilePath;

    public static DirectoryInfo GameDataDirectory =>
        _gameDataDirectory ??= new DirectoryInfo(Application.dataPath).Parent.CreateSubdirectory("GameData");

    public static string RuntimeStateFilePath =>
        _runtimeStateFilePath ??= AetheriaRuntimeStateBoot.Inspect(GameDataDirectory).StateFilePath;

    public static void ClearRuntimeStateFilePathCache()
    {
        _runtimeStateFilePath = null;
    }
}
