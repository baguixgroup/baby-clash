using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
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

        /// <summary>
        /// Xcode project for iPad (and iPhone). Add -simulator for the iOS Simulator. Open the project in Xcode, pick your
        /// team under Signing &amp; Capabilities, and run it on a device.
        /// </summary>
        public static void BuildIOS()
        {
            BuildReport report = Build(BuildTarget.iOS, CommandLinePath(Path.Combine(DefaultFolder, HasArgument("-simulator") ? "iOS-Simulator" : "iOS")));
            if (report.summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        /// <summary>Android APK (add -aab for a Google Play app bundle). Signed with Unity's debug key unless a keystore is set.</summary>
        public static void BuildAndroid()
        {
            string file = HasArgument("-aab") ? "BabyClash.aab" : "BabyClash.apk";
            BuildReport report = Build(BuildTarget.Android, CommandLinePath(Path.Combine(DefaultFolder, "Android", file)));
            if (report.summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        [MenuItem("Baby Clash/Build iPad Xcode Project", priority = 42)]
        static void BuildIOSFromMenu()
        {
            BuildReport report = Build(BuildTarget.iOS, Path.Combine(DefaultFolder, "iOS"));
            if (report.summary.result == BuildResult.Succeeded)
                EditorUtility.RevealInFinder(report.summary.outputPath);
        }

        [MenuItem("Baby Clash/Build Android APK", priority = 43)]
        static void BuildAndroidFromMenu()
        {
            BuildReport report = Build(BuildTarget.Android, Path.Combine(DefaultFolder, "Android", "BabyClash.apk"));
            if (report.summary.result == BuildResult.Succeeded)
                EditorUtility.RevealInFinder(report.summary.outputPath);
        }

        /// <summary>Tablet settings shared by iOS and Android: landscape only, same app id, full screen.</summary>
        static void ConfigureMobile(BuildTarget target)
        {
            const string appId = "com.babyclash.game";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, appId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, appId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.statusBarHidden = true;

            if (target == BuildTarget.iOS)
            {
                PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
                PlayerSettings.iOS.targetOSVersionString = "15.0";
                PlayerSettings.iOS.requiresFullScreen = true;
                PlayerSettings.iOS.sdkVersion = HasArgument("-simulator") ? iOSSdkVersion.SimulatorSDK : iOSSdkVersion.DeviceSDK;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            }
            else if (target == BuildTarget.Android)
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                // ARM64: every current phone and tablet, and what Google Play requires (Unity 6.6 dropped x86-64).
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
                PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
                EditorUserBuildSettings.buildAppBundle = HasArgument("-aab");
            }
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
            else if (target == BuildTarget.iOS || target == BuildTarget.Android)
            {
                ConfigureMobile(target);
            }

            // Release unless asked: a stale "Development" tick in the editor would show the developer console on devices.
            EditorUserBuildSettings.development = HasArgument("-development");
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
