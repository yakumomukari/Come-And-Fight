using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class SteamDevelopmentBuildPostprocessor : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        string source = Path.Combine(Directory.GetCurrentDirectory(), "steam_appid.txt");
        string destination = Path.Combine(Path.GetDirectoryName(report.summary.outputPath) ?? string.Empty, "steam_appid.txt");
        if (!File.Exists(source))
        {
            Debug.LogWarning("[Steam] Development build is missing steam_appid.txt.");
            return;
        }
        File.Copy(source, destination, true);
        Debug.Log("[Steam] Copied test App ID next to the executable. Remove this file before a Steam release build.");
    }
}
