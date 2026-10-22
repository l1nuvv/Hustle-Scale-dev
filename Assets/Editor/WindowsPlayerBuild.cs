using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Explicit Windows x64 batch build; does not change project settings or scene lists.</summary>
public static class WindowsPlayerBuild
{
    [Serializable]
    private sealed class Report
    {
        public string unity, result, output, backend, api, stripping, defines;
        public string[] scenes;
        public bool development;
        public int errors, warnings;
        public ulong bytes;
        public double seconds;
    }

    // Unity -batchmode -quit -buildTarget StandaloneWindows64
    // -executeMethod WindowsPlayerBuild.Build -buildOutput <absolute-path.exe>
    // Optional: -firstDealFixture, -developmentBuild.
    public static void Build()
    {
        try
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-buildOutput");
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
                throw new BuildFailedException("-buildOutput requires an absolute .exe path.");
            var output = Path.GetFullPath(args[index + 1]);
            var project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var directory = Path.GetDirectoryName(output);
            if (!string.Equals(Path.GetExtension(output), ".exe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(directory, project, StringComparison.OrdinalIgnoreCase)
                || new[] { "Assets", "Packages", "ProjectSettings", "Library", ".git" }.Any(folder =>
                    output.StartsWith(Path.Combine(project, folder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                throw new BuildFailedException("Choose a dedicated build directory outside project source, settings and caches.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                throw new BuildFailedException("Launch Unity with -buildTarget StandaloneWindows64.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new BuildFailedException("Windows Build Support is unavailable in this Editor.");
            if (BuildProfile.GetActiveBuildProfile() != null)
                throw new BuildFailedException("This entry point uses the shared scene list; launch without a saved active Build Profile.");

            var scenes = args.Contains("-firstDealFixture")
                ? new[] { "Assets/Scenes/FirstDealTrade.unity" }
                : EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0 || scenes.Any(scene => AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null))
                throw new BuildFailedException("The selected build scene list is empty or contains a missing scene.");
            var development = args.Contains("-developmentBuild");
            var options = BuildOptions.DetailedBuildReport;
            if (development) options |= BuildOptions.Development;
            Directory.CreateDirectory(directory);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                target = BuildTarget.StandaloneWindows64,
                locationPathName = output,
                options = options
            });
            var target = NamedBuildTarget.Standalone;
            var summary = report.summary;
            File.WriteAllText(output + ".build-report.json", JsonUtility.ToJson(new Report
            {
                unity = Application.unityVersion,
                result = summary.result.ToString(),
                output = output,
                backend = PlayerSettings.GetScriptingBackend(target).ToString(),
                api = PlayerSettings.GetApiCompatibilityLevel(target).ToString(),
                stripping = PlayerSettings.GetManagedStrippingLevel(target).ToString(),
                defines = PlayerSettings.GetScriptingDefineSymbols(target),
                scenes = scenes,
                development = development,
                errors = summary.totalErrors,
                warnings = summary.totalWarnings,
                bytes = summary.totalSize,
                seconds = summary.totalTime.TotalSeconds
            }, true));
            Debug.Log($"WindowsPlayerBuild: {summary.result}; errors={summary.totalErrors}, warnings={summary.totalWarnings}, bytes={summary.totalSize}");
            if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
                throw new BuildFailedException("Windows Player build failed; inspect the Editor log and build-report.json.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else throw;
        }
    }
}
