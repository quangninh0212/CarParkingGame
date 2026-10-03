using System;
using System.IO;
using CarParkingGame.Missions;
using CarParkingGame.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarParkingGame.EditorTools
{
    // Renders what each camera mode actually shows, per car, to PNGs.
    //
    // The driver's eye point is computed from the car's body bounds, and a number that
    // looks sensible in the Inspector can still put the camera under the dashboard or
    // inside the roof. This is the only way to check it without a device.
    //
    // It never saves the scene.
    public static class VehicleViewShotTool
    {
        private const string ScenePath = "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity";

        public static void CaptureFromCommandLine()
        {
            string directory = GetArg("-shotDir") ?? "view-shots";
            int width = int.TryParse(GetArg("-shotWidth"), out int w) ? w : 1600;
            int height = int.TryParse(GetArg("-shotHeight"), out int h) ? h : 720;

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError($"[VehicleViewShotTool] Could not open '{ScenePath}'.");
                EditorApplication.Exit(1);
                return;
            }

            Directory.CreateDirectory(directory);

            Camera camera = Camera.main;

            if (camera == null)
            {
                Debug.LogError("[VehicleViewShotTool] No main camera.");
                EditorApplication.Exit(1);
                return;
            }

            camera.gameObject.SetActive(true);
            camera.enabled = true;
            camera.nearClipPlane = 0.05f;
            camera.aspect = width / (float)height;

            // Somewhere with scenery in front of the car, so "can you see out" is a real
            // question rather than an empty horizon.
            Transform start = FindStartPoint();

            CarController[] cars = UnityEngine.Object.FindObjectsByType<CarController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (CarController car in cars)
            {
                CaptureCar(car, cars, start, camera, width, height, directory);
            }

            Debug.Log($"[VehicleViewShotTool] Wrote driver views to '{directory}'.");
            EditorApplication.Exit(0);
        }

        private static void CaptureCar(
            CarController car,
            CarController[] allCars,
            Transform start,
            Camera camera,
            int width,
            int height,
            string directory)
        {
            foreach (CarController other in allCars)
            {
                other.gameObject.SetActive(other == car);
            }

            if (start != null)
            {
                car.transform.SetPositionAndRotation(start.position, start.rotation);
            }

            var points = car.GetComponent<VehicleViewPoints>();

            if (points == null)
            {
                points = car.gameObject.AddComponent<VehicleViewPoints>();
            }

            // Overridable from the command line so the seat can be swept over a few values
            // and the results compared, instead of guessing one and rebuilding.
            if (TryGetFloat("-eyeHeight", out float eyeHeight))
            {
                points.EditorSetFractions(
                    TryGetFloat("-eyeSide", out float side) ? side : -0.42f,
                    TryGetFloat("-eyeLength", out float length) ? length : 0.3f,
                    eyeHeight,
                    TryGetFloat("-rearLength", out float rearLength) ? rearLength : -0.3f,
                    TryGetFloat("-rearHeight", out float rearHeight) ? rearHeight : 0.8f);
            }

            points.Measure();

            float pitch = TryGetFloat("-pitch", out float p) ? p : 4f;
            float rearPitch = TryGetFloat("-rearPitch", out float rp) ? rp : 10f;

            Debug.Log($"[VehicleViewShotTool] '{car.name}' eye {points.DriverEyeLocalPosition:0.000} rear {points.RearViewLocalPosition:0.000}.");

            Capture(camera, car.transform, points.DriverEyeLocalPosition, Quaternion.Euler(pitch, 0f, 0f), width, height,
                Path.Combine(directory, car.name + "-cockpit.png"));

            Capture(camera, car.transform, points.RearViewLocalPosition, Quaternion.Euler(rearPitch, 180f, 0f), width, height,
                Path.Combine(directory, car.name + "-rear.png"));
        }

        private static Transform FindStartPoint()
        {
            var manager = UnityEngine.Object.FindFirstObjectByType<MissionManager>(FindObjectsInactive.Include);

            if (manager == null)
            {
                return null;
            }

            manager.RebuildRegistry();

            MissionAuthoring mission = manager.GetMission(1);

            if (mission != null)
            {
                mission.SetEnvironmentActive(true);
            }

            return mission != null ? mission.StartPoint : null;
        }

        private static void Capture(
            Camera camera,
            Transform car,
            Vector3 localEye,
            Quaternion localRotation,
            int width,
            int height,
            string path)
        {
            camera.transform.SetPositionAndRotation(car.TransformPoint(localEye), car.rotation * localRotation);

            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousActive = RenderTexture.active;

            camera.targetTexture = target;
            camera.Render();

            RenderTexture.active = target;

            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();

            File.WriteAllBytes(path, image.EncodeToPNG());

            camera.targetTexture = null;
            RenderTexture.active = previousActive;

            UnityEngine.Object.DestroyImmediate(image);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }

        private static bool TryGetFloat(string name, out float value)
        {
            return float.TryParse(GetArg(name), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value);
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
