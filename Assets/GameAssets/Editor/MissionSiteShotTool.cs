using System;
using System.Collections.Generic;
using System.IO;
using CarParkingGame.Missions;
using CarParkingGame.Parking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarParkingGame.EditorTools
{
    // Renders every mission's bay from directly above onto one contact sheet.
    //
    // The sites are chosen by raycasting, which proves the ground is flat, drivable and
    // clear - and proves nothing about whether the bay landed in the middle of the racing
    // line or half under a grandstand roof. This is the look at it.
    //
    // It never saves the scene.
    public static class MissionSiteShotTool
    {
        private const string ScenePath = "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity";

        public static void CaptureFromCommandLine()
        {
            string output = GetArg("-sheetOut") ?? "mission-sites.png";
            int tile = int.TryParse(GetArg("-tile"), out int t) ? t : 300;
            float span = float.TryParse(GetArg("-span"), out float s) ? s : 22f;

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError($"[MissionSiteShotTool] Could not open '{ScenePath}'.");
                EditorApplication.Exit(1);
                return;
            }

            var manager = UnityEngine.Object.FindFirstObjectByType<MissionManager>(FindObjectsInactive.Include);

            if (manager == null)
            {
                Debug.LogError("[MissionSiteShotTool] No MissionManager.");
                EditorApplication.Exit(1);
                return;
            }

            manager.RebuildRegistry();

            var missions = new List<MissionAuthoring>(manager.RegisteredMissions);

            // One mission, rendered into the whole sheet, for looking at a course closely
            // rather than judging it from a thumbnail.
            string only = GetArg("-only");

            if (!string.IsNullOrEmpty(only) && int.TryParse(only, out int onlyId))
            {
                missions.RemoveAll(mission => mission.MissionId != onlyId);
            }

            if (missions.Count == 0)
            {
                Debug.LogError("[MissionSiteShotTool] No missions registered.");
                EditorApplication.Exit(1);
                return;
            }

            // Every mission's props on at once: the sites are distinct places now, so they
            // can all stand up together, and a bay that overlaps another one shows here.
            foreach (MissionAuthoring mission in missions)
            {
                mission.SetEnvironmentActive(true);
            }

            Camera camera = PrepareCamera(span);

            int columns = Mathf.Min(6, missions.Count);
            int rows = Mathf.CeilToInt(missions.Count / (float)columns);
            var sheet = new Texture2D(columns * tile, rows * tile, TextureFormat.RGB24, false);

            var order = new List<string>();

            for (int i = 0; i < missions.Count; i++)
            {
                MissionAuthoring mission = missions[i];
                ParkingZone zone = mission.ParkingZone;

                if (zone == null)
                {
                    continue;
                }

                // Frame the whole mission, not a fixed box round the bay: the courses
                // differ in size by a factor of five and a shared span either crops the
                // big ones or loses the small ones in the grass.
                Vector3 target = zone.WorldCenter;
                float frame = span;

                if (TryMeasureMission(mission, out Bounds bounds))
                {
                    target = bounds.center;
                    frame = Mathf.Max(bounds.size.x, bounds.size.z) * 1.15f;

                    Debug.Log($"[MissionSiteShotTool] Mission {mission.MissionId}: bounds centre {bounds.center:0.0} size {bounds.size:0.0} -> frame {frame:0.0}m.");
                }

                camera.orthographicSize = frame * 0.5f;

                Color[] pixels = Render(camera, target, tile);

                int column = i % columns;

                // Rows fill from the top, and texture rows count up from the bottom.
                int row = rows - 1 - i / columns;

                sheet.SetPixels(column * tile, row * tile, tile, tile, pixels);
                order.Add($"{mission.MissionId:00}");
            }

            sheet.Apply();
            File.WriteAllBytes(output, sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);

            Debug.Log($"[MissionSiteShotTool] Wrote '{output}'. Tiles read left to right, top to bottom: {string.Join(", ", order)}");
            EditorApplication.Exit(0);
        }

        private static bool TryMeasureMission(MissionAuthoring mission, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;

            GameObject container = mission.EnvironmentContainer != null ? mission.EnvironmentContainer : mission.gameObject;

            foreach (Renderer renderer in container.GetComponentsInChildren<Renderer>(true))
            {
                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return any;
        }

        private static Camera PrepareCamera(float span)
        {
            Camera camera = Camera.main;

            foreach (Camera candidate in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.GetComponent<CarCameraController>() == null)
                {
                    camera = candidate;
                    break;
                }
            }

            camera.gameObject.SetActive(true);
            camera.enabled = true;
            camera.orthographic = true;
            camera.orthographicSize = span * 0.5f;
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 400f;
            camera.aspect = 1f;

            return camera;
        }

        // Looking straight down reads the geometry but not the heights, and grey walls on
        // a grey floor vanish. A pitch below 90 gives an oblique view where walls, cones
        // and parked cars are all obvious.
        private static float PitchDegrees =>
            float.TryParse(GetArg("-pitch"), out float pitch) ? pitch : 90f;

        private static Color[] Render(Camera camera, Vector3 target, int size)
        {
            float pitch = PitchDegrees;
            float distance = Mathf.Max(60f, camera.orthographicSize * 2f);

            Quaternion rotation = Quaternion.Euler(pitch, 0f, 0f);

            // Back off along the way the camera is looking. Offsetting the other way put
            // it under the ground, which is why the oblique sheet came out as mud.
            Vector3 position = target - rotation * Vector3.forward * distance;

            camera.transform.SetPositionAndRotation(position, rotation);

            var buffer = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousActive = RenderTexture.active;

            camera.targetTexture = buffer;
            camera.Render();

            RenderTexture.active = buffer;

            var image = new Texture2D(size, size, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            image.Apply();

            Color[] pixels = image.GetPixels();

            camera.targetTexture = null;
            RenderTexture.active = previousActive;

            UnityEngine.Object.DestroyImmediate(image);
            buffer.Release();
            UnityEngine.Object.DestroyImmediate(buffer);

            return pixels;
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
