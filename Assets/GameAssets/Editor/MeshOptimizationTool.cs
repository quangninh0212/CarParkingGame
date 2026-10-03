using System.IO;
using UnityEditor;
using UnityEngine;

namespace CarParkingGame.EditorTools
{
    // Meshes became the biggest part of the build once the textures were compressed
    // (150 MB, ~70%), dominated by the track pack's scenery models. Mesh compression is
    // lossy on vertex precision, so it is applied only to models whose source file is
    // large, and the result needs a look in the Editor.
    //
    // Revertible with `git checkout -- Assets`, since this only changes .meta files.
    public static class MeshOptimizationTool
    {
        private const long SizeThresholdBytes = 4 * 1024 * 1024;

        [MenuItem("Tools/Car Parking/Optimize Meshes For Android")]
        public static void Optimize()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/GameAssets" });
            int changed = 0;
            long bytes = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var info = new FileInfo(path);

                if (!info.Exists || info.Length < SizeThresholdBytes)
                {
                    continue;
                }

                if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                {
                    continue;
                }

                importer.meshCompression = ModelImporterMeshCompression.High;
                importer.optimizeMeshVertices = true;
                importer.optimizeMeshPolygons = true;
                importer.weldVertices = true;

                // The cars need their rigs; scenery does not need anything animated.
                if (importer.animationType == ModelImporterAnimationType.None)
                {
                    importer.importAnimation = false;
                }

                importer.SaveAndReimport();

                bytes += info.Length;
                changed++;
                Debug.Log($"  {info.Length / (1024 * 1024)} MB -> compressed: {path}");
            }

            Debug.Log($"[MeshOptimizationTool] Compressed {changed} model(s) totalling {bytes / (1024 * 1024)} MB of source art. Check the track visually; revert with: git checkout -- Assets");
        }

        public static void OptimizeFromCommandLine()
        {
            Optimize();
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }
    }
}
