using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarParkingGame.EditorTools
{
    // Dumps the gameplay scene's hierarchy and components to a text file so the scene
    // can be inspected without opening the Editor. Read-only: it never writes the scene.
    public static class SceneReportTool
    {
        private const string ScenePath = "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity";

        public static void ReportFromCommandLine()
        {
            string output = GetArg("-reportOut") ?? "scene-report.txt";
            int maxDepth = int.TryParse(GetArg("-reportDepth"), out int d) ? d : 4;

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError($"[SceneReportTool] Could not open '{ScenePath}'.");
                EditorApplication.Exit(1);
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine($"SCENE: {scene.name}  roots={scene.rootCount}");

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Describe(root.transform, 0, maxDepth, builder);
            }

            File.WriteAllText(output, builder.ToString());
            Debug.Log($"[SceneReportTool] Wrote {output}");
            EditorApplication.Exit(0);
        }

        private static void Describe(Transform t, int depth, int maxDepth, StringBuilder builder)
        {
            var components = new List<string>();

            foreach (Component component in t.GetComponents<Component>())
            {
                if (component == null)
                {
                    components.Add("<missing>");
                    continue;
                }

                if (component is Transform)
                {
                    continue;
                }

                string extra = string.Empty;

                if (component is Collider collider)
                {
                    extra = collider.isTrigger ? "(trigger)" : "(solid)";
                }

                if (component is Renderer renderer)
                {
                    var names = new List<string>();

                    foreach (Material material in renderer.sharedMaterials)
                    {
                        names.Add(material != null ? material.name : "<none>");
                    }

                    extra = "[" + string.Join("|", names) + "]";
                }

                components.Add(component.GetType().Name + extra);
            }

            builder.Append(new string(' ', depth * 2));
            builder.Append(t.name);
            builder.Append(t.gameObject.activeSelf ? string.Empty : " [off]");
            builder.Append($" tag={t.gameObject.tag} layer={LayerMask.LayerToName(t.gameObject.layer)}");
            builder.Append($" pos={t.localPosition:0.00} scale={t.localScale:0.00}");

            if (components.Count > 0)
            {
                builder.Append("  {" + string.Join(", ", components) + "}");
            }

            builder.Append($"  children={t.childCount}");
            builder.AppendLine();

            if (depth >= maxDepth)
            {
                return;
            }

            for (int i = 0; i < t.childCount; i++)
            {
                Describe(t.GetChild(i), depth + 1, maxDepth, builder);
            }
        }

        private static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
