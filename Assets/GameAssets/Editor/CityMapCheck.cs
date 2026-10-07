using CarParkingGame.Core;
using CarParkingGame.Missions;
using CarParkingGame.UI;
using CarParkingGame.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CarParkingGame.EditorTools
{
    // Checks the city free-drive map is there, wired, and startable.
    //
    // Everything here is something that fails silently. A city left switched on in the
    // saved scene costs a million triangles in every car park level and looks no different
    // in the editor. A spawn inside a wall only shows when someone presses the button. A
    // chunk with no collider is a hole the car falls through, somewhere, once.
    public static class CityMapCheck
    {
        private const string ScenePath = "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity";

        public static void RunFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            int failures = 0;

            GameObject root = FindRoot();

            if (root == null)
            {
                Debug.LogError("[CityMapCheck] No 'CityMap' in the scene; run Tools/Car Parking/Build City Free Drive Map.");
                EditorApplication.Exit(1);
                return;
            }

            // Off in the saved scene. Practice and Challenge never switch it on, so if it
            // ships active every car park level pays for the whole city.
            if (root.activeSelf)
            {
                Debug.LogError("[CityMapCheck] The city is switched on in the saved scene; every level would pay for it.");
                failures++;
            }

            failures += CheckChunks(root);
            failures += CheckWiring(root);
            failures += CheckSpawn(root);
            failures += CheckReachable(root);
            failures += CheckApart(root);

            Debug.Log(failures == 0
                ? "[CityMapCheck] The city map is built, wired, off by default and the car can start in it."
                : $"[CityMapCheck] {failures} failure(s).");

            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static GameObject FindRoot()
        {
            foreach (GameObject found in Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (found.name == CityMapBuilder.RootName && found.transform.parent == null)
                {
                    return found;
                }
            }

            return null;
        }

        private static int CheckChunks(GameObject root)
        {
            int chunks = 0;
            int noCollider = 0;
            int noMaterial = 0;
            long triangles = 0;

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                chunks++;
                triangles += filter.sharedMesh.triangles.Length / 3;

                if (filter.GetComponent<MeshCollider>() == null)
                {
                    Debug.LogError($"[CityMapCheck] '{filter.name}' has no collider: the car falls through it.");
                    noCollider++;
                }

                var renderer = filter.GetComponent<MeshRenderer>();

                if (renderer == null)
                {
                    continue;
                }

                // A submesh with no material renders magenta, and with seventy two of them
                // in the source a single slip paints a whole district.
                if (renderer.sharedMaterials.Length != filter.sharedMesh.subMeshCount)
                {
                    Debug.LogError($"[CityMapCheck] '{filter.name}' has {renderer.sharedMaterials.Length} material(s) "
                        + $"for {filter.sharedMesh.subMeshCount} submesh(es).");
                    noMaterial++;
                }

                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        Debug.LogError($"[CityMapCheck] '{filter.name}' has an empty material slot: it renders magenta.");
                        noMaterial++;
                        break;
                    }
                }
            }

            if (chunks == 0)
            {
                Debug.LogError("[CityMapCheck] The city has no chunks in it.");
                return 1;
            }

            Debug.Log($"[CityMapCheck] {chunks} chunk(s), {triangles} triangles, all with colliders and materials.");
            return noCollider + noMaterial;
        }

        private static int CheckWiring(GameObject root)
        {
            int failures = 0;

            var session = Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);

            if (session == null)
            {
                Debug.LogError("[CityMapCheck] No GameSession.");
                return 1;
            }

            var serialized = new SerializedObject(session);

            Object wiredRoot = serialized.FindProperty("cityMapRoot").objectReferenceValue;
            Object wiredSpawn = serialized.FindProperty("cityRoamSpawn").objectReferenceValue;

            if (wiredRoot != root)
            {
                Debug.LogError("[CityMapCheck] GameSession does not point at the city; picking the city would start the circuit.");
                failures++;
            }

            if (wiredSpawn == null || !((Transform)wiredSpawn).IsChildOf(root.transform))
            {
                Debug.LogError("[CityMapCheck] GameSession has no spawn inside the city.");
                failures++;
            }

            var menu = Object.FindFirstObjectByType<MenuController>(FindObjectsInactive.Include);

            if (menu == null)
            {
                Debug.LogError("[CityMapCheck] No MenuController.");
                return failures + 1;
            }

            var menuSerialized = new SerializedObject(menu);

            foreach (string field in new[] { "freeDriveScreen", "circuitMapButton", "cityMapButton" })
            {
                SerializedProperty property = menuSerialized.FindProperty(field);

                if (property == null || property.objectReferenceValue == null)
                {
                    Debug.LogError($"[CityMapCheck] MenuController.{field} is not assigned; the map chooser is dead.");
                    failures++;
                }
            }

            return failures;
        }

        // The spawn has to be a place the car can actually stand: ground under it, and
        // nothing already occupying the space it would appear in.
        private static int CheckSpawn(GameObject root)
        {
            var session = Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);
            var serialized = new SerializedObject(session);
            var spawn = serialized.FindProperty("cityRoamSpawn").objectReferenceValue as Transform;

            if (spawn == null)
            {
                return 0;
            }

            bool wasActive = root.activeSelf;
            root.SetActive(true);
            Physics.SyncTransforms();

            int failures = 0;

            if (!Physics.Raycast(spawn.position + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 50f)
                || !hit.collider.transform.IsChildOf(root.transform))
            {
                Debug.LogError("[CityMapCheck] Nothing solid under the city spawn: the car falls out of the world.");
                failures++;
            }
            else if (Mathf.Abs(hit.point.y - spawn.position.y) > 1f)
            {
                Debug.LogError($"[CityMapCheck] The city spawn is {hit.point.y - spawn.position.y:0.0}m off the ground.");
                failures++;
            }

            failures += CheckEveryCarStands(root, session);

            root.SetActive(wasActive);
            return failures;
        }

        // Puts each car where the game would put it and checks it is standing on the road.
        //
        // The earlier version of this tested a car-sized box centred on the spawn marker,
        // which is not where any car ends up and so passed while the game dropped the
        // classic into the tarmac and threw it onto its roof. This runs the real placement -
        // the same VehicleStance call GameSession makes - and then looks at the car.
        private static int CheckEveryCarStands(GameObject root, GameSession session)
        {
            int failures = 0;
            int checkedCars = 0;

            CarController[] cars = Object.FindObjectsByType<CarController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            var wasActive = new bool[cars.Length];
            var wasAt = new Vector3[cars.Length];
            var wasTurned = new Quaternion[cars.Length];

            for (int i = 0; i < cars.Length; i++)
            {
                wasActive[i] = cars[i].gameObject.activeSelf;
                wasAt[i] = cars[i].transform.position;
                wasTurned[i] = cars[i].transform.rotation;
            }

            bool wasInCity = session.InCity;

            for (int i = 0; i < cars.Length; i++)
            {
                CarController car = cars[i];

                // One at a time. The game drives whichever car is active, so leaving two of
                // them on would place whichever of the pair happened to be found first.
                for (int other = 0; other < cars.Length; other++)
                {
                    cars[other].gameObject.SetActive(other == i);
                }

                Transform body = car.transform.Find("BodyCollider");
                var shell = body != null ? body.GetComponent<Collider>() : null;

                if (shell == null)
                {
                    continue;
                }

                // The game's own placement, not a copy of it. Reaching for the private
                // method through reflection is worth it: a check that calls the helper
                // directly still passes when the call to that helper is taken back out of
                // the game, which is exactly the regression worth catching here.
                ActiveVehicleLocator.Invalidate();
                SetInCity(session, true);
                PlaceCar(session);
                Physics.SyncTransforms();

                Vector3 placed = car.transform.position;

                checkedCars++;

                float wheels = placed.y - RideHeight(car.transform);
                float ground = GroundUnder(placed, root, car.transform);

                if (Mathf.Abs(wheels - ground) > 0.15f)
                {
                    Debug.LogError($"[CityMapCheck] '{car.name}' would be placed with its wheels "
                        + $"{wheels - ground:0.00}m from the road: it is dropped and lands on its roof.");
                    failures++;
                }

                // Nothing of the city inside the bodywork. An overlap is resolved by the
                // physics engine the only way it can be, which is to fire the car out of it.
                foreach (Collider collider in root.GetComponentsInChildren<MeshCollider>(true))
                {
                    if (!Physics.ComputePenetration(
                        shell, shell.transform.position, shell.transform.rotation,
                        collider, collider.transform.position, collider.transform.rotation,
                        out Vector3 _, out float depth) || depth <= 0.02f)
                    {
                        continue;
                    }

                    Debug.LogError($"[CityMapCheck] '{car.name}' starts {depth:0.00}m inside '{collider.name}'.");
                    failures++;
                    break;
                }

            }

            // Everything back where it was found. This check moves the player's cars about
            // and switches them on and off, and the scene it does that in is the one the
            // game ships.
            for (int i = 0; i < cars.Length; i++)
            {
                cars[i].transform.SetPositionAndRotation(wasAt[i], wasTurned[i]);
                cars[i].gameObject.SetActive(wasActive[i]);
            }

            SetInCity(session, wasInCity);
            ActiveVehicleLocator.Invalidate();
            Physics.SyncTransforms();

            if (failures == 0)
            {
                Debug.Log($"[CityMapCheck] All {checkedCars} car(s) stand on the road at the city spawn.");
            }

            return failures;
        }

        private static void SetInCity(GameSession session, bool value)
        {
            System.Reflection.FieldInfo field = typeof(GameSession).GetField(
                "inCity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            field?.SetValue(session, value);
        }

        private static void PlaceCar(GameSession session)
        {
            System.Reflection.MethodInfo method = typeof(GameSession).GetMethod(
                "PlaceCarAtFreeRoamSpawn",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            method?.Invoke(session, null);
        }

        private static float RideHeight(Transform vehicle)
        {
            Renderer[] renderers = vehicle.GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
            {
                return 0f;
            }

            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return Mathf.Max(0f, vehicle.position.y - bounds.min.y);
        }

        // The road under a point, ignoring the car standing on it. Without that exclusion
        // the ray hits the car first, finds it is not part of the city, and reports no road
        // at all - which is a check that fails on everything and so says nothing.
        private static float GroundUnder(Vector3 point, GameObject root, Transform vehicle)
        {
            RaycastHit[] hits = Physics.RaycastAll(
                point + Vector3.up * 3f, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);

            float best = float.MinValue;

            foreach (RaycastHit hit in hits)
            {
                if (!hit.collider.transform.IsChildOf(vehicle)
                    && hit.collider.transform.IsChildOf(root.transform)
                    && hit.point.y > best)
                {
                    best = hit.point.y;
                }
            }

            return best;
        }

        // How much of the city the player can actually reach from where they start.
        //
        // Standing on a road proves nothing about whether that road goes anywhere. A spawn
        // in a courtyard, or on an island of pavement behind a kerb the car cannot climb,
        // passes every other check here and is still a map with nothing in it. This walks
        // outwards from the spawn across ground the car could drive over and measures what
        // it can get to.
        private static int CheckReachable(GameObject root)
        {
            var session = Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);
            var serialized = new SerializedObject(session);
            var spawn = serialized.FindProperty("cityRoamSpawn").objectReferenceValue as Transform;

            if (spawn == null)
            {
                return 0;
            }

            bool wasActive = root.activeSelf;
            root.SetActive(true);
            Physics.SyncTransforms();

            const float Step = 3f;
            const float MaxRise = 0.35f;
            const int Limit = 20000;

            var heights = new System.Collections.Generic.Dictionary<Vector2Int, float>();
            var queue = new System.Collections.Generic.Queue<Vector2Int>();
            var seen = new System.Collections.Generic.HashSet<Vector2Int>();

            Vector2Int start = Cell(spawn.position, Step);

            if (!Ground(start, Step, root, out float startY))
            {
                root.SetActive(wasActive);
                return 0;
            }

            heights[start] = startY;
            seen.Add(start);
            queue.Enqueue(start);

            var steps = new[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1)
            };

            while (queue.Count > 0 && seen.Count < Limit)
            {
                Vector2Int cell = queue.Dequeue();
                float from = heights[cell];

                foreach (Vector2Int offset in steps)
                {
                    Vector2Int next = cell + offset;

                    if (!seen.Add(next))
                    {
                        continue;
                    }

                    if (!Ground(next, Step, root, out float y) || Mathf.Abs(y - from) > MaxRise)
                    {
                        continue;
                    }

                    heights[next] = y;
                    queue.Enqueue(next);
                }
            }

            root.SetActive(wasActive);

            float area = heights.Count * Step * Step;

            // The model has about 64,000 square metres of flat ground in it. Reaching only a
            // few thousand of that means the car is shut in somewhere.
            if (area < 8000f)
            {
                Debug.LogError($"[CityMapCheck] Only {area:0} m2 is reachable from the spawn: the car is shut in.");
                return 1;
            }

            Debug.Log($"[CityMapCheck] {area:0} m2 of the city is drivable from the spawn without leaving street level.");
            return 0;
        }

        private static Vector2Int Cell(Vector3 point, float step)
        {
            return new Vector2Int(Mathf.RoundToInt(point.x / step), Mathf.RoundToInt(point.z / step));
        }

        private static bool Ground(Vector2Int cell, float step, GameObject root, out float y)
        {
            y = 0f;

            var from = new Vector3(cell.x * step, root.transform.position.y + 60f, cell.y * step);

            if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, 200f)
                || !hit.collider.transform.IsChildOf(root.transform))
            {
                return false;
            }

            // Street level only. Without this the walk climbs the bridge ramps onto the
            // deck and then along every rooftop it can reach, and reports the whole model.
            if (Mathf.Abs(hit.point.y - root.transform.position.y) > 1.5f)
            {
                return false;
            }

            y = hit.point.y;
            return true;
        }

        // The city sits in its own corner of the world. If it ever overlapped a car park,
        // Practice would be played inside a building.
        private static int CheckApart(GameObject root)
        {
            Bounds city = default;
            bool first = true;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (first)
                {
                    city = renderer.bounds;
                    first = false;
                }
                else
                {
                    city.Encapsulate(renderer.bounds);
                }
            }

            var manager = Object.FindFirstObjectByType<MissionManager>(FindObjectsInactive.Include);

            if (manager == null)
            {
                return 0;
            }

            manager.RebuildRegistry();

            foreach (MissionAuthoring mission in manager.RegisteredMissions)
            {
                if (mission.StartPoint != null && city.Contains(mission.StartPoint.position))
                {
                    Debug.LogError($"[CityMapCheck] Mission {mission.MissionId} starts inside the city.");
                    return 1;
                }
            }

            Debug.Log($"[CityMapCheck] The city stands clear of every car park, {city.size.x:0} x {city.size.z:0} metres at {city.center:0}.");
            return 0;
        }
    }
}
