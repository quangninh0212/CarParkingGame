using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CarParkingGame.EditorTools
{
    // Batch-mode development APK build, for verifying that the project still builds for
    // Android. Changes no project settings: it builds whatever is already configured.
    //
    // Output goes to CARPARKING_APK_PATH when that environment variable is set, so a CI or
    // scratch build can keep the APK out of the repository.
    public static class AndroidBuildScript
    {
        [MenuItem("Tools/Car Parking/Build Development APK")]
        public static void BuildDevelopmentApk()
        {
            string[] scenes = CollectEnabledScenes();

            if (scenes.Length == 0)
            {
                Debug.LogError("[AndroidBuildScript] No enabled scenes in Build Settings; nothing to build.");
                return;
            }

            string outputPath = ResolveOutputPath();
            string directory = Path.GetDirectoryName(outputPath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            Debug.Log($"[AndroidBuildScript] Building {scenes.Length} scene(s) to '{outputPath}'.");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log($"[AndroidBuildScript] Result: {summary.result}, errors: {summary.totalErrors}, warnings: {summary.totalWarnings}, size: {summary.totalSize / (1024 * 1024)} MB, time: {summary.totalTime}");

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[AndroidBuildScript] Build did not succeed.");
            }
        }

        // Release build: no development flag, so managed code stripping applies. Used to
        // measure the real shipping size against the development build.
        public static void BuildReleaseFromCommandLine()
        {
            BuildFromCommandLine(BuildOptions.None, "release");
        }

        public static void BuildFromCommandLine()
        {
            BuildFromCommandLine(BuildOptions.Development, "development");
        }

        private static void BuildFromCommandLine(BuildOptions buildOptions, string label)
        {
            int exitCode = 1;

            try
            {
                string[] scenes = CollectEnabledScenes();

                if (scenes.Length == 0)
                {
                    Debug.LogError("[AndroidBuildScript] No enabled scenes in Build Settings; nothing to build.");
                    EditorApplication.Exit(1);
                    return;
                }

                string outputPath = ResolveOutputPath();
                string directory = Path.GetDirectoryName(outputPath);

                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = buildOptions
                };

                BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;

                Debug.Log($"[AndroidBuildScript] Result ({label}): {summary.result}, errors: {summary.totalErrors}, time: {summary.totalTime}, apk: '{outputPath}'");

                exitCode = summary.result == BuildResult.Succeeded ? 0 : 1;
            }
            catch (Exception error)
            {
                Debug.LogError($"[AndroidBuildScript] Build threw: {error}");
            }

            EditorApplication.Exit(exitCode);
        }

        private static string[] CollectEnabledScenes()
        {
            var scenes = new List<string>();

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    scenes.Add(scene.path);
                }
            }

            return scenes.ToArray();
        }

        private static string ResolveOutputPath()
        {
            string fromEnvironment = Environment.GetEnvironmentVariable("CARPARKING_APK_PATH");

            return string.IsNullOrEmpty(fromEnvironment)
                ? Path.Combine("Builds", "CarParkingGame.apk")
                : fromEnvironment;
        }
    }
}
