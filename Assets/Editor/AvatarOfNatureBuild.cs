using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AvatarOfNatureBuild
{
    public static void BuildWindows64()
    {
        const string outputDirectory = "Builds/AvatarOfNature-Windows64";
        const string executablePath = outputDirectory + "/AvatarOfNature.exe";

        Directory.CreateDirectory(outputDirectory);
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled && File.Exists(scene.path))
                scenes.Add(scene);
        }

        if (scenes.Count == 0)
            throw new BuildFailedException("No enabled scenes are configured in Build Settings.");

        var options = new BuildPlayerOptions
        {
            scenes = scenes.ConvertAll(scene => scene.path).ToArray(),
            locationPathName = executablePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.StrictMode
        };

        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"[AvatarOfNatureBuild] Result={report.summary.result}; Size={report.summary.totalSize} bytes; Errors={report.summary.totalErrors}; Warnings={report.summary.totalWarnings}");

        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException($"Windows build failed: {report.summary.result}");
    }
}
