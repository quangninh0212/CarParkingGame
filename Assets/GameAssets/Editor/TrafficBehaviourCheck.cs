using System.Collections.Generic;
using System.Reflection;
using CarParkingGame.Traffic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarParkingGame.EditorTools
{
    // Drives a traffic car down a road and checks it stops for what is in front of it.
    //
    // Traffic is the one part of the game that cannot be judged from a photograph: a
    // screenshot of a car halfway through a parked one and a screenshot of a car about to
    // brake look the same. A player found both faults here - traffic driving through
    // stationary cars, and traffic ignoring a red - and neither showed up in any check.
    //
    // So the car is ticked by hand, a step at a time, with real colliders in a real scene.
    // TrafficVehicle.Tick takes its own delta, which is what makes that possible outside
    // play mode; Awake is called through reflection because the editor does not run it for
    // an object instantiated like this.
    public static class TrafficBehaviourCheck
    {
        private const string ScenePath = "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity";
        private const string PrefabFolder = "Assets/GameAssets/Prefabs/Traffic";

        private const float Step = 0.04f;
        private const int Steps = 600;

        public static void RunFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            int failures = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));

                if (prefab == null || prefab.GetComponent<TrafficVehicle>() == null)
                {
                    continue;
                }

                failures += CheckStopsBehindAParkedCar(prefab);
                failures += CheckStopsAtARed(prefab);
            }

            Debug.Log(failures == 0
                ? "[TrafficBehaviourCheck] Traffic stops behind what is in front of it and holds at a red."
                : $"[TrafficBehaviourCheck] {failures} failure(s).");

            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        // A stationary car across the road ahead. The driver has to stop short of it.
        private static int CheckStopsBehindAParkedCar(GameObject prefab)
        {
            var scratch = new GameObject("TrafficProbe");

            TrafficVehicle driver = Spawn(prefab, scratch.transform, new Vector3(0f, 0f, 0f));
            TrafficVehicle standing = Spawn(prefab, scratch.transform, new Vector3(0f, 0f, 60f));

            WaypointPath path = StraightRoad(scratch.transform, 120f, null);

            Wake(driver);
            Wake(standing);
            Physics.SyncTransforms();

            driver.Initialize(path, 0, 0f);

            float closest = float.MaxValue;

            for (int i = 0; i < Steps; i++)
            {
                driver.Tick(Step);
                Physics.SyncTransforms();

                closest = Mathf.Min(closest, standing.transform.position.z - driver.transform.position.z);
            }

            Object.DestroyImmediate(scratch);

            // Nose to nose, not overlapping. The gap is measured pivot to pivot, so it has
            // to clear both cars' half lengths; anything under a couple of metres means one
            // has driven into the other.
            if (closest < 3f)
            {
                Debug.LogError($"[TrafficBehaviourCheck] '{prefab.name}' closed to {closest:0.00}m "
                    + "of a stationary car: it drives through it.");
                return 1;
            }

            Debug.Log($"[TrafficBehaviourCheck] OK '{prefab.name}' stopped {closest:0.00}m behind a stationary car.");
            return 0;
        }

        // A red light over a waypoint partway down the road. The driver has to hold at it
        // rather than slow down and carry on through.
        private static int CheckStopsAtARed(GameObject prefab)
        {
            var scratch = new GameObject("TrafficProbe");

            var head = new GameObject("Light");
            head.transform.SetParent(scratch.transform, false);
            head.transform.position = new Vector3(3f, 0f, 60f);

            // Red is the state a light starts in, so nothing has to be driven to get one.
            TrafficLight light = head.AddComponent<TrafficLight>();

            TrafficVehicle driver = Spawn(prefab, scratch.transform, Vector3.zero);

            // Long enough that the car cannot reach the end of it in the time it is ticked
            // for. A car that runs out of road turns and comes back, and where it happened
            // to be when the ticking stopped then said nothing about the light - which is
            // how a broken-on-purpose build passed this.
            WaypointPath path = StraightRoad(scratch.transform, 300f, (point, z) =>
            {
                if (Mathf.Abs(z - 60f) > 0.1f)
                {
                    return;
                }

                var data = point.AddComponent<TrafficWaypoint>();
                var serialized = new SerializedObject(data);
                serialized.FindProperty("governingLight").objectReferenceValue = light;
                serialized.ApplyModifiedProperties();
            });

            Wake(driver);
            Physics.SyncTransforms();

            driver.Initialize(path, 0, 0f);

            // The furthest it ever got, not where it ended up: a car that rolls through a
            // red and then stops is still a car that ran it.
            float furthest = float.MinValue;

            for (int i = 0; i < Steps; i++)
            {
                driver.Tick(Step);
                Physics.SyncTransforms();

                furthest = Mathf.Max(furthest, driver.transform.position.z);
            }

            float past = furthest - 60f;
            Object.DestroyImmediate(scratch);

            if (past > 1.5f)
            {
                Debug.LogError($"[TrafficBehaviourCheck] '{prefab.name}' went {past:0.00}m past a red light.");
                return 1;
            }

            if (past < -8f)
            {
                Debug.LogError($"[TrafficBehaviourCheck] '{prefab.name}' stopped {-past:0.00}m short of a red light, "
                    + "which is far enough back to look broken.");
                return 1;
            }

            Debug.Log($"[TrafficBehaviourCheck] OK '{prefab.name}' held at a red, {-past:0.00}m short of the line.");
            return 0;
        }

        private static TrafficVehicle Spawn(GameObject prefab, Transform parent, Vector3 at)
        {
            GameObject instance = Object.Instantiate(prefab, parent);
            instance.transform.SetPositionAndRotation(at, Quaternion.identity);

            return instance.GetComponent<TrafficVehicle>();
        }

        private static WaypointPath StraightRoad(Transform parent, float length,
            System.Action<GameObject, float> dress)
        {
            var host = new GameObject("Road");
            host.transform.SetParent(parent, false);

            var path = host.AddComponent<WaypointPath>();
            var points = new List<Transform>();

            for (float z = 10f; z <= length; z += 10f)
            {
                var point = new GameObject($"Waypoint {points.Count:00}");
                point.transform.SetParent(host.transform, false);
                point.transform.position = new Vector3(0f, 0f, z);

                dress?.Invoke(point, z);
                points.Add(point.transform);
            }

            var serialized = new SerializedObject(path);
            SerializedProperty array = serialized.FindProperty("waypoints");
            array.arraySize = points.Count;

            for (int i = 0; i < points.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            }

            serialized.FindProperty("loop").boolValue = false;
            serialized.ApplyModifiedProperties();

            return path;
        }

        private static void Wake(MonoBehaviour behaviour)
        {
            MethodInfo awake = behaviour.GetType().GetMethod(
                "Awake", BindingFlags.Instance | BindingFlags.NonPublic);

            awake?.Invoke(behaviour, null);
        }
    }
}
