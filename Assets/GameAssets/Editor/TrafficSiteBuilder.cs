using System.Collections.Generic;
using CarParkingGame.Missions;
using CarParkingGame.Traffic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarParkingGame.EditorTools
{
    // Puts traffic on the training site.
    //
    // The site is forty-two walled car parks packed in rows, and the packer already leaves
    // eight metres between them. Those gaps were empty. They are streets now, and the wall
    // round each lot is only 1.15m high - so from inside any of the forty-two, the player
    // sees cars going past. One road network, every level livelier.
    //
    // Nothing here can reach the player. The lots are sealed, so traffic is scenery that
    // cannot drive into a mission, block a bay or score a collision. That is the whole
    // reason it is safe to add this late.
    //
    // The lanes are measured off the courses themselves rather than recomputed, so adding
    // or removing a level moves the streets with it.
    public static class TrafficSiteBuilder
    {
        private const string ScenePath = "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity";
        private const string RootName = "SiteTraffic";

        // The first level built on the training site. Below it the missions are patches of
        // the circuit and have nothing to do with this.
        private const int FirstSiteMission = 9;

        private const float WaypointSpacing = 14f;
        private const float RowTolerance = 12f;
        private const float LaneEdgeMargin = 5f;

        private static readonly string[] CarPrefabs =
        {
            "Assets/GameAssets/Hatchback and Sedan/prefabs/SEDAN.prefab",
            "Assets/GameAssets/Hatchback and Sedan/prefabs/HATCHBACK_1988.prefab",
            "Assets/GameAssets/50s, 60s and 70s Car Pack (6 Cars)/1950s Classic Car/Prefabs/ClassicCarFull.prefab"
        };

        [MenuItem("Tools/Car Parking/Build Site Traffic")]
        public static void RunInOpenScene()
        {
            Build();
            Debug.Log("[TrafficSiteBuilder] Done. Save the scene to keep it.");
        }

        public static void RunFromCommandLine()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError($"[TrafficSiteBuilder] Could not open '{ScenePath}'.");
                EditorApplication.Exit(1);
                return;
            }

            bool ok = Build();

            if (ok)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Build()
        {
            if (!TryMeasureSite(out List<Bounds> courses, out Bounds site))
            {
                return false;
            }

            Remove();

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build site traffic");

            List<float> lanes = Lanes(courses, site);

            if (lanes.Count < 2)
            {
                Debug.LogError($"[TrafficSiteBuilder] Only {lanes.Count} lane(s) between the car parks; nothing to drive on.");
                return false;
            }

            if (!LanesAreClear(lanes, courses))
            {
                Object.DestroyImmediate(root);
                return false;
            }

            BuildRoadSurface(root.transform, lanes, site);

            WaypointPath path = BuildSnake(root.transform, lanes, site, out List<TrafficWaypoint> entrances);

            BuildLights(root.transform, entrances, site);
            BuildManager(root.transform, path);

            Debug.Log($"[TrafficSiteBuilder] {lanes.Count} street(s) across a {site.size.x:0}x{site.size.z:0}m site, " +
                      $"{path.Count} waypoints, {entrances.Count} light(s).");

            return true;
        }

        private static void Remove()
        {
            GameObject existing = GameObject.Find(RootName);

            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }
        }

        // ----- measuring the site -------------------------------------------------------

        private static bool TryMeasureSite(out List<Bounds> courses, out Bounds site)
        {
            courses = new List<Bounds>();
            site = default;

            var manager = Object.FindFirstObjectByType<MissionManager>(FindObjectsInactive.Include);

            if (manager == null)
            {
                Debug.LogError("[TrafficSiteBuilder] No MissionManager in the scene.");
                return false;
            }

            manager.RebuildRegistry();

            bool any = false;

            foreach (MissionAuthoring mission in manager.RegisteredMissions)
            {
                if (mission.MissionId < FirstSiteMission)
                {
                    continue;
                }

                // Bounds of a renderer on a deactivated object are not to be trusted, so
                // each course is switched on long enough to be measured and put back.
                bool wasActive = mission.gameObject.activeSelf;
                mission.SetEnvironmentActive(true);

                if (TryMeasure(mission.transform, out Bounds bounds))
                {
                    courses.Add(bounds);

                    if (!any)
                    {
                        site = bounds;
                        any = true;
                    }
                    else
                    {
                        site.Encapsulate(bounds);
                    }
                }

                mission.gameObject.SetActive(wasActive);
            }

            if (!any)
            {
                Debug.LogError($"[TrafficSiteBuilder] No missions from {FirstSiteMission} up could be measured.");
                return false;
            }

            return true;
        }

        private static bool TryMeasure(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
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

        // The z of every street: one down the middle of each gap between two rows of car
        // parks, and one along each end of the site.
        private static List<float> Lanes(List<Bounds> courses, Bounds site)
        {
            courses.Sort((a, b) => a.center.z.CompareTo(b.center.z));

            var rows = new List<Vector2>();
            float rowMin = courses[0].min.z;
            float rowMax = courses[0].max.z;

            for (int i = 1; i < courses.Count; i++)
            {
                // Same row while the next course starts before the current row ends, give
                // or take: the packer lines a row up but the courses in it differ in size.
                if (courses[i].center.z - (rowMin + rowMax) * 0.5f < RowTolerance)
                {
                    rowMin = Mathf.Min(rowMin, courses[i].min.z);
                    rowMax = Mathf.Max(rowMax, courses[i].max.z);
                    continue;
                }

                rows.Add(new Vector2(rowMin, rowMax));
                rowMin = courses[i].min.z;
                rowMax = courses[i].max.z;
            }

            rows.Add(new Vector2(rowMin, rowMax));

            var lanes = new List<float> { rows[0].x - LaneEdgeMargin };

            for (int i = 0; i < rows.Count - 1; i++)
            {
                lanes.Add((rows[i].y + rows[i + 1].x) * 0.5f);
            }

            lanes.Add(rows[rows.Count - 1].y + LaneEdgeMargin);
            return lanes;
        }

        // A street runs the full width of the site, so it must not cross a single car
        // park. Checked rather than assumed: the rows are grouped by a tolerance, and a
        // tolerance that is wrong by a metre puts a road through somebody's level.
        private static bool LanesAreClear(List<float> lanes, List<Bounds> courses)
        {
            // Half a street width either side of the centre line.
            const float HalfWidth = 3f;

            foreach (float lane in lanes)
            {
                foreach (Bounds course in courses)
                {
                    if (lane + HalfWidth <= course.min.z || lane - HalfWidth >= course.max.z)
                    {
                        continue;
                    }

                    Debug.LogError(
                        $"[TrafficSiteBuilder] A street at z={lane:0.0} runs through a car park " +
                        $"spanning z {course.min.z:0.0} to {course.max.z:0.0}.");

                    return false;
                }
            }

            return true;
        }

        // Asphalt under each street with a broken line down the middle of it.
        //
        // The streets were traffic driving over bare site slab. A surface and a centre line
        // is what tells the player, over the wall of the lot they are parked in, that the
        // thing out there is a road.
        private static void BuildRoadSurface(Transform parent, List<float> lanes, Bounds site)
        {
            var host = new GameObject("Streets");
            host.transform.SetParent(parent, false);

            var kit = new MissionCourseKit();

            float left = site.min.x - LaneEdgeMargin;
            float right = site.max.x + LaneEdgeMargin;
            float width = right - left;
            float y = site.min.y;

            const float RoadWidth = 7f;

            foreach (float lane in lanes)
            {
                // Just proud of the slab, the way the bay paint is: level with it and the
                // two surfaces fight for the same pixels.
                GameObject road = kit.Spawn(MissionCourseKit.Part.Concrete, host.transform,
                    new Vector3((left + right) * 0.5f, y + 0.03f, lane), 0f,
                    new Vector3(width, 0.06f, RoadWidth));

                MissionCourseKit.Tint(road, "SiteRoad", new Color(0.22f, 0.22f, 0.24f));

                kit.PaintLine(host.transform,
                    new Vector3(left + 2f, y + 0.06f, lane),
                    new Vector3(right - 2f, y + 0.06f, lane),
                    true, 0.22f, 3f, 3f);
            }
        }

        // ----- the road ------------------------------------------------------------------

        // One long loop snaking along every street and back, so traffic appears in every
        // gap on the site without a junction graph to get wrong. Cars spread themselves
        // along it, which from inside any one car park looks like a road going past.
        private static WaypointPath BuildSnake(Transform parent, List<float> lanes, Bounds site,
            out List<TrafficWaypoint> entrances)
        {
            var host = new GameObject("SiteLoop");
            host.transform.SetParent(parent, false);

            var path = host.AddComponent<WaypointPath>();
            var points = new List<Transform>();

            entrances = new List<TrafficWaypoint>();

            float left = site.min.x - LaneEdgeMargin;
            float right = site.max.x + LaneEdgeMargin;
            float y = site.min.y;

            for (int lane = 0; lane < lanes.Count; lane++)
            {
                bool rightwards = (lane & 1) == 0;

                float from = rightwards ? left : right;
                float to = rightwards ? right : left;

                int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(to - from) / WaypointSpacing));

                for (int i = 0; i <= steps; i++)
                {
                    float x = Mathf.Lerp(from, to, i / (float)steps);
                    Transform point = Point(host.transform, points.Count, new Vector3(x, y, lanes[lane]));

                    points.Add(point);

                    // One light per street, a little way in from the end it is entered
                    // from, so a queue has somewhere to form.
                    if (i == 2)
                    {
                        entrances.Add(point.GetComponent<TrafficWaypoint>());
                    }
                }
            }

            var serialized = new SerializedObject(path);
            SerializedProperty array = serialized.FindProperty("waypoints");

            array.arraySize = points.Count;

            for (int i = 0; i < points.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            }

            serialized.FindProperty("loop").boolValue = true;
            serialized.ApplyModifiedProperties();

            return path;
        }

        private static Transform Point(Transform parent, int index, Vector3 at)
        {
            var point = new GameObject($"Waypoint {index:000}");
            point.transform.SetParent(parent, false);
            point.transform.position = at;

            point.AddComponent<TrafficWaypoint>();
            return point.transform;
        }

        // ----- the lights -----------------------------------------------------------------

        private static void BuildLights(Transform parent, List<TrafficWaypoint> entrances, Bounds site)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TrafficLightGenerator.PrefabPath);

            if (prefab == null)
            {
                TrafficLightGenerator.Generate();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TrafficLightGenerator.PrefabPath);
            }

            if (prefab == null || entrances.Count == 0)
            {
                Debug.LogWarning("[TrafficSiteBuilder] No traffic light prefab; the streets get no lights.");
                return;
            }

            var host = new GameObject("TrafficLights");
            host.transform.SetParent(parent, false);

            var group = host.AddComponent<TrafficLightGroup>();

            var even = new List<TrafficLight>();
            var odd = new List<TrafficLight>();

            for (int i = 0; i < entrances.Count; i++)
            {
                TrafficWaypoint waypoint = entrances[i];

                if (waypoint == null)
                {
                    continue;
                }

                // Beside the street, set back from the line the cars drive, facing the
                // traffic that is about to reach it.
                Vector3 at = waypoint.transform.position + new Vector3(0f, 0f, 4f);

                GameObject head = (GameObject)PrefabUtility.InstantiatePrefab(prefab, host.transform);
                head.transform.position = at;
                head.transform.rotation = Quaternion.Euler(0f, (i & 1) == 0 ? 90f : -90f, 0f);

                var light = head.GetComponent<TrafficLight>();

                var serialized = new SerializedObject(waypoint);
                serialized.FindProperty("governingLight").objectReferenceValue = light;
                serialized.FindProperty("isCrossing").boolValue = true;
                serialized.ApplyModifiedProperties();

                // Alternating streets on alternating phases, so half the site is moving
                // while the other half waits rather than everything stopping at once.
                (((i & 1) == 0) ? even : odd).Add(light);
            }

            SetPhases(group, even, odd);
        }

        private static void SetPhases(TrafficLightGroup group, List<TrafficLight> even, List<TrafficLight> odd)
        {
            var serialized = new SerializedObject(group);
            SerializedProperty phases = serialized.FindProperty("phases");

            phases.arraySize = 2;

            FillPhase(phases.GetArrayElementAtIndex(0), "Odd streets", even);
            FillPhase(phases.GetArrayElementAtIndex(1), "Even streets", odd);

            serialized.ApplyModifiedProperties();
        }

        private static void FillPhase(SerializedProperty phase, string name, List<TrafficLight> lights)
        {
            phase.FindPropertyRelative("name").stringValue = name;

            SerializedProperty array = phase.FindPropertyRelative("lights");
            array.arraySize = lights.Count;

            for (int i = 0; i < lights.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
            }
        }

        // ----- the cars --------------------------------------------------------------------

        private static void BuildManager(Transform parent, WaypointPath path)
        {
            var host = new GameObject("TrafficManager");
            host.transform.SetParent(parent, false);

            var manager = host.AddComponent<TrafficManager>();
            var pool = new GameObject("Pool");
            pool.transform.SetParent(host.transform, false);

            var prefabs = new List<TrafficVehicle>();

            foreach (string carPath in CarPrefabs)
            {
                TrafficVehicle prefab = TrafficCarPrefab(carPath);

                if (prefab != null)
                {
                    prefabs.Add(prefab);
                }
            }

            if (prefabs.Count == 0)
            {
                Debug.LogWarning("[TrafficSiteBuilder] No traffic car prefabs could be made; the streets stay empty.");
            }

            var serialized = new SerializedObject(manager);
            SerializedProperty cars = serialized.FindProperty("vehiclePrefabs");

            cars.arraySize = prefabs.Count;

            for (int i = 0; i < prefabs.Count; i++)
            {
                cars.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            }

            SerializedProperty paths = serialized.FindProperty("paths");
            paths.arraySize = 1;
            paths.GetArrayElementAtIndex(0).objectReferenceValue = path;

            serialized.FindProperty("poolParent").objectReferenceValue = pool.transform;
            serialized.ApplyModifiedProperties();
        }

        private const string TrafficPrefabFolder = "Assets/GameAssets/Prefabs/Traffic";

        // A traffic car is one of the player's car models with everything that made it a
        // player car taken off it: no controller, no rigidbody, no wheel colliders. It is
        // moved along a path, not driven, so physics on it would only fight the path.
        private static TrafficVehicle TrafficCarPrefab(string sourcePath)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);

            if (source == null)
            {
                Debug.LogWarning($"[TrafficSiteBuilder] No car at '{sourcePath}'.");
                return null;
            }

            string name = "Traffic_" + System.IO.Path.GetFileNameWithoutExtension(sourcePath);
            string path = $"{TrafficPrefabFolder}/{name}.prefab";

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (existing != null)
            {
                return existing.GetComponent<TrafficVehicle>();
            }

            GameObject instance = Object.Instantiate(source);
            instance.name = name;

            foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Object.DestroyImmediate(behaviour);
            }

            foreach (WheelCollider wheel in instance.GetComponentsInChildren<WheelCollider>(true))
            {
                Object.DestroyImmediate(wheel);
            }

            foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true))
            {
                Object.DestroyImmediate(body);
            }

            // Its colliders stay. They are what the sensor on the car behind reads, which
            // is how a queue forms at a red light rather than a pile.
            var vehicle = instance.AddComponent<TrafficVehicle>();

            System.IO.Directory.CreateDirectory(TrafficPrefabFolder);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);

            return saved != null ? saved.GetComponent<TrafficVehicle>() : vehicle;
        }
    }
}
