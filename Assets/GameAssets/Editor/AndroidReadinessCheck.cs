using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarParkingGame.EditorTools
{
    // Automates the Android checklist: orientation, bundle id, SDK levels, architectures,
    // scripting backend, quality levels, build scenes and the render pipeline asset.
    // Reports rather than gates - it is meant to be run before a release build.
    public static class AndroidReadinessCheck
    {
        private const string TemplateIdentifierFragment = "UnityTechnologies";

        [MenuItem("Tools/Car Parking/Check Android Readiness")]
        public static void Run()
        {
            var problems = new List<string>();
            var notes = new List<string>();

            CheckIdentity(problems, notes);
            CheckOrientation(problems, notes);
            CheckBuildConfiguration(problems, notes);
            CheckQuality(problems, notes);
            CheckRenderPipeline(problems, notes);
            CheckScenes(problems, notes);

            foreach (string note in notes)
            {
                Debug.Log("[AndroidReadinessCheck] " + note);
            }

            if (problems.Count == 0)
            {
                Debug.Log("[AndroidReadinessCheck] No problems found.");
                return;
            }

            Debug.LogWarning($"[AndroidReadinessCheck] {problems.Count} thing(s) to fix before shipping:");

            foreach (string problem in problems)
            {
                Debug.LogWarning("  - " + problem);
            }
        }

        public static void RunFromCommandLine()
        {
            Run();
            EditorApplication.Exit(0);
        }

        private static void CheckIdentity(List<string> problems, List<string> notes)
        {
            string identifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            notes.Add($"Android application id: {identifier}");

            if (identifier.Contains(TemplateIdentifierFragment) || identifier.Contains("Template"))
            {
                problems.Add($"The Android application id is still the Unity template default ('{identifier}'). Change it before any store upload.");
            }

            if (PlayerSettings.companyName == "DefaultCompany")
            {
                problems.Add("Company name is still 'DefaultCompany'.");
            }
        }

        private static void CheckOrientation(List<string> problems, List<string> notes)
        {
            notes.Add($"Default orientation: {PlayerSettings.defaultInterfaceOrientation}");

            if (PlayerSettings.allowedAutorotateToPortrait || PlayerSettings.allowedAutorotateToPortraitUpsideDown)
            {
                problems.Add("Portrait autorotation is still allowed; this game is landscape only.");
            }

            if (!PlayerSettings.allowedAutorotateToLandscapeLeft && !PlayerSettings.allowedAutorotateToLandscapeRight)
            {
                problems.Add("Neither landscape orientation is allowed, so the game has no valid orientation.");
            }
        }

        private static void CheckBuildConfiguration(List<string> problems, List<string> notes)
        {
            notes.Add($"Min SDK: {PlayerSettings.Android.minSdkVersion}, target SDK: {PlayerSettings.Android.targetSdkVersion}");
            notes.Add($"Scripting backend: {PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)}");
            notes.Add($"Target architectures: {PlayerSettings.Android.targetArchitectures}");
            notes.Add($"Graphics APIs: {string.Join(", ", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android))}");
            notes.Add($"Active build target: {EditorUserBuildSettings.activeBuildTarget}");

            if (PlayerSettings.Android.targetSdkVersion == AndroidSdkVersions.AndroidApiLevelAuto)
            {
                problems.Add("Android target SDK is set to Automatic. Pin it explicitly so store compliance does not change under you.");
            }

            if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
            {
                problems.Add("ARM64 is not in the target architectures; Google Play requires it.");
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                problems.Add($"The active build target is {EditorUserBuildSettings.activeBuildTarget}, not Android.");
            }
        }

        private static void CheckQuality(List<string> problems, List<string> notes)
        {
            string[] levels = QualitySettings.names;
            notes.Add($"Quality levels: {string.Join(", ", levels)}");

            int androidDefault = ReadAndroidDefaultQualityLevel();

            if (androidDefault < 0)
            {
                notes.Add("Could not read the per-platform default quality level; check it by hand in Project Settings > Quality.");
                return;
            }

            notes.Add($"Android default quality level index: {androidDefault}");

            if (androidDefault >= levels.Length)
            {
                problems.Add($"Android's default quality level is index {androidDefault} but only {levels.Length} level(s) exist. Unity will clamp it, so Android is not reliably starting on the level you intended.");
            }
        }

        private static int ReadAndroidDefaultQualityLevel()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");

            if (assets == null || assets.Length == 0 || assets[0] == null)
            {
                return -1;
            }

            SerializedProperty perPlatform = new SerializedObject(assets[0]).FindProperty("m_PerPlatformDefaultQuality");

            if (perPlatform == null || !perPlatform.isArray)
            {
                return -1;
            }

            for (int i = 0; i < perPlatform.arraySize; i++)
            {
                SerializedProperty element = perPlatform.GetArrayElementAtIndex(i);
                SerializedProperty key = element.FindPropertyRelative("first");
                SerializedProperty value = element.FindPropertyRelative("second");

                if (key != null && value != null && key.stringValue == "Android")
                {
                    return value.intValue;
                }
            }

            return -1;
        }

        private static void CheckRenderPipeline(List<string> problems, List<string> notes)
        {
            RenderPipelineAsset pipeline = GraphicsSettings.defaultRenderPipeline;

            if (pipeline == null)
            {
                problems.Add("No default render pipeline asset is assigned in Graphics settings.");
                return;
            }

            notes.Add($"Default render pipeline asset: {pipeline.name}");

            if (pipeline.name.Contains("PC"))
            {
                problems.Add($"The global default render pipeline asset is '{pipeline.name}'. Per-quality-level overrides still apply, but the global fallback should be the mobile one.");
            }
        }

        private static void CheckScenes(List<string> problems, List<string> notes)
        {
            int enabled = 0;

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    enabled++;
                    notes.Add($"Build scene: {scene.path}");
                }
            }

            if (enabled == 0)
            {
                problems.Add("No scenes are enabled in Build Settings.");
            }
        }
    }
}
