using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CarParkingGame.EditorTools
{
    // Gives the heavy textures Android import overrides. The release build is 79.5%
    // textures, with a single uncompressed road.psd accounting for 256 MB of it, so this is
    // where the APK size actually lives - the game code is about 0.3%.
    //
    // Only textures whose source file is over the size threshold are touched, so the blast
    // radius stays on the actual offenders. Import settings live in .meta files, which are
    // tracked by git, so this is revertible with `git checkout -- Assets`.
    //
    // It changes how the game looks. Run it, then look at the track in the Editor.
    public static class TextureOptimizationTool
    {
        // Above this source-file size, a texture is considered worth overriding.
        private const long SizeThresholdBytes = 2 * 1024 * 1024;

        private const int DefaultMaxSize = 1024;

        // The road and garage surfaces are what the player stares at, so they keep more
        // resolution than the scenery.
        private const int DetailMaxSize = 2048;

        private static readonly string[] DetailKeywords = { "road", "garage" };

        // Pixel-art UI must not be compressed or downscaled; it is tiny anyway.
        private static readonly string[] SkipFolders = { "SimplePixelUI", "/Sprites/", "TutorialInfo" };

        [MenuItem("Tools/Car Parking/Optimize Textures For Android")]
        public static void Optimize()
        {
            int changed = 0;
            long bytesConsidered = 0;
            var report = new List<string>();

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/GameAssets" });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (ShouldSkip(path))
                {
                    continue;
                }

                var info = new FileInfo(path);

                if (!info.Exists || info.Length < SizeThresholdBytes)
                {
                    continue;
                }

                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                {
                    continue;
                }

                int maxSize = IsDetailTexture(path) ? DetailMaxSize : DefaultMaxSize;

                var settings = new TextureImporterPlatformSettings
                {
                    name = "Android",
                    overridden = true,
                    maxTextureSize = maxSize,
                    format = TextureImporterFormat.ASTC_6x6,
                    compressionQuality = (int)TextureCompressionQuality.Normal,
                    textureCompression = TextureImporterCompression.Compressed,
                    allowsAlphaSplitting = false
                };

                importer.SetPlatformTextureSettings(settings);
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();

                bytesConsidered += info.Length;
                changed++;
                report.Add($"  {info.Length / (1024 * 1024)} MB -> max {maxSize}px ASTC 6x6: {path}");
            }

            Debug.Log($"[TextureOptimizationTool] Overrode {changed} texture(s) totalling {bytesConsidered / (1024 * 1024)} MB of source art.");

            foreach (string line in report)
            {
                Debug.Log(line);
            }

            if (changed > 0)
            {
                Debug.Log("[TextureOptimizationTool] Look at the track in the Editor before shipping. Revert with: git checkout -- Assets");
            }
        }

        public static void OptimizeFromCommandLine()
        {
            Optimize();
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }

        private static bool ShouldSkip(string path)
        {
            foreach (string folder in SkipFolders)
            {
                if (path.Contains(folder))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDetailTexture(string path)
        {
            string lower = path.ToLowerInvariant();

            foreach (string keyword in DetailKeywords)
            {
                if (lower.Contains(keyword))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
