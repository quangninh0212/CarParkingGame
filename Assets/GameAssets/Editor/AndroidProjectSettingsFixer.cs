using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarParkingGame.EditorTools
{
    // Fixes the two unambiguous Android configuration bugs the project audit found.
    //
    // A menu item rather than something applied automatically: these are project-wide
    // render settings, and the person running it should be able to look at the result.
    // Identity and SDK decisions (bundle id, company name, target SDK) are left alone
    // because they are release choices, not bugs.
    public static class AndroidProjectSettingsFixer
    {
        private const string MobileQualityLevelName = "Mobile";
        private const string MobilePipelineAssetPath = "Assets/Settings/Mobile_RPAsset.asset";

        [MenuItem("Tools/Car Parking/Fix Android Project Settings")]
        public static void Fix()
        {
            bool changed = FixAndroidDefaultQualityLevel();
            changed |= FixDefaultRenderPipelineAsset();

            if (!changed)
            {
                Debug.Log("[AndroidProjectSettingsFixer] Nothing needed fixing.");
                return;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[AndroidProjectSettingsFixer] Done. Re-run Check Android Readiness to confirm, and look at the game in the Editor to confirm rendering is unchanged.");
        }

        // The project defines only two quality levels but Android's default was index 2.
        // Unity clamps it silently, so Android was not reliably starting on "Mobile".
        private static bool FixAndroidDefaultQualityLevel()
        {
            int mobileIndex = FindQualityLevelIndex(MobileQualityLevelName);

            if (mobileIndex < 0)
            {
                Debug.LogWarning($"[AndroidProjectSettingsFixer] No quality level named '{MobileQualityLevelName}'; left the Android default alone.");
                return false;
            }

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");

            if (assets == null || assets.Length == 0 || assets[0] == null)
            {
                Debug.LogWarning("[AndroidProjectSettingsFixer] Could not open QualitySettings; fix the Android default quality level by hand in Project Settings > Quality.");
                return false;
            }

            var settings = new SerializedObject(assets[0]);
            SerializedProperty perPlatform = settings.FindProperty("m_PerPlatformDefaultQuality");

            if (perPlatform == null || !perPlatform.isArray)
            {
                Debug.LogWarning("[AndroidProjectSettingsFixer] Could not read the per-platform quality map; fix it by hand.");
                return false;
            }

            for (int i = 0; i < perPlatform.arraySize; i++)
            {
                SerializedProperty element = perPlatform.GetArrayElementAtIndex(i);
                SerializedProperty key = element.FindPropertyRelative("first");
                SerializedProperty value = element.FindPropertyRelative("second");

                if (key == null || value == null || key.stringValue != "Android")
                {
                    continue;
                }

                if (value.intValue == mobileIndex)
                {
                    return false;
                }

                Debug.Log($"[AndroidProjectSettingsFixer] Android default quality level {value.intValue} -> {mobileIndex} ('{MobileQualityLevelName}').");
                value.intValue = mobileIndex;
                settings.ApplyModifiedProperties();
                return true;
            }

            Debug.LogWarning("[AndroidProjectSettingsFixer] No Android entry in the per-platform quality map; set it by hand in Project Settings > Quality.");
            return false;
        }

        private static bool FixDefaultRenderPipelineAsset()
        {
            var mobilePipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(MobilePipelineAssetPath);

            if (mobilePipeline == null)
            {
                Debug.LogWarning($"[AndroidProjectSettingsFixer] No render pipeline asset at '{MobilePipelineAssetPath}'; left the global default alone.");
                return false;
            }

            if (GraphicsSettings.defaultRenderPipeline == mobilePipeline)
            {
                return false;
            }

            string previous = GraphicsSettings.defaultRenderPipeline != null
                ? GraphicsSettings.defaultRenderPipeline.name
                : "none";

            GraphicsSettings.defaultRenderPipeline = mobilePipeline;

            Debug.Log($"[AndroidProjectSettingsFixer] Global default render pipeline '{previous}' -> '{mobilePipeline.name}'.");
            return true;
        }

        private static int FindQualityLevelIndex(string levelName)
        {
            string[] names = QualitySettings.names;

            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == levelName)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
