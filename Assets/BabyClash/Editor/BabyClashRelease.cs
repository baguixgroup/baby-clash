using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BabyClash.EditorTools
{
    /// <summary>
    /// One-click release builds (menu "Baby Clash/Build ...") and a command-line entry point for CI:
    /// Unity -batchmode -quit -projectPath . -executeMethod BabyClash.EditorTools.BabyClashRelease.BuildMacOS [-buildPath path]
    /// </summary>
    public static class BabyClashRelease
    {
        const string DefaultFolder = "Builds";

        [MenuItem("Baby Clash/Build macOS App", priority = 40)]
        static void BuildMacOSFromMenu()
        {
            BuildReport report = Build(BuildTarget.StandaloneOSX, Path.Combine(DefaultFolder, "macOS", "Baby Clash.app"));
            if (report.summary.result == BuildResult.Succeeded)
                EditorUtility.RevealInFinder(report.summary.outputPath);
        }

        [MenuItem("Baby Clash/Build Windows App", priority = 41)]
        static void BuildWindowsFromMenu()
        {
            BuildReport report = Build(BuildTarget.StandaloneWindows64, Path.Combine(DefaultFolder, "Windows", "BabyClash.exe"));
            if (report.summary.result == BuildResult.Succeeded)
                EditorUtility.RevealInFinder(report.summary.outputPath);
        }

        public static void BuildMacOS()
        {
            BuildReport report = Build(BuildTarget.StandaloneOSX, CommandLinePath(Path.Combine(DefaultFolder, "macOS", "Baby Clash.app")));
            if (report.summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        public static void BuildWindows()
        {
            BuildReport report = Build(BuildTarget.StandaloneWindows64, CommandLinePath(Path.Combine(DefaultFolder, "Windows", "BabyClash.exe")));
            if (report.summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        public static void BuildRaspberryPi()
        {
            string path = CommandLinePath(Path.Combine(DefaultFolder, "RaspberryPi"));
            BuildReport report = RaspberryPiBuild.Build(path);
            if (report.summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        internal static BuildReport Build(BuildTarget target, string path)
        {
            if (!BabyClashContentBuilder.IsContentCurrent())
                BabyClashContentBuilder.BuildAll();

            if (target == BuildTarget.StandaloneOSX)
            {
                // Universal binary: runs natively on Intel and Apple Silicon Macs.
                EditorUserBuildSettings.SetPlatformSettings("Standalone", "OSXUniversal", "Architecture", "x64ARM64");
            }
            else if (target == BuildTarget.StandaloneWindows64)
            {
                EditorUserBuildSettings.SetPlatformSettings(BuildPipeline.GetBuildTargetName(target), "Architecture", "x64");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { BabyClashContentBuilder.ScenePath },
                locationPathName = path,
                target = target,
                // -development: profiler markers for performance investigations (see CommandLineSmokeTest -profileMarkers).
                options = HasArgument("-development") ? BuildOptions.Development : BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"[Baby Clash] {target} build {summary.result}: {summary.outputPath} " +
                      $"({summary.totalSize / (1024f * 1024f):0.0} MB, {summary.totalTime.TotalSeconds:0}s, {summary.totalErrors} errors)");
            return report;
        }

        static bool HasArgument(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        static string CommandLinePath(string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-buildPath")
                    return args[i + 1];
            }
            return fallback;
        }
    }
}
