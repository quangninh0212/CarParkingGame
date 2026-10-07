using System.Collections.Generic;
using System.IO;
using CarParkingGame.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarParkingGame.EditorTools
{
    // Builds the city into the scene as a second free-drive map.
    //
    // The model is two baked meshes, one of them 1.09 million triangles. Nothing can be
    // culled out of a single mesh, so one corner of the city on screen costs the whole
    // million every frame - and a MeshCollider over all of it in one piece is a single
    // enormous cook. It is cut into a grid so the renderer can throw away what is behind
    // the camera and so each piece carries its own collider.
    //
    // The cut leaves every chunk exactly where it was. That is the whole difference from
    // the surround round the car parks, where chunks were recentred on their own base so
    // they could be set down somewhere else - which is what tore them. Here a triangle
    // assigned to one cell is drawn at the same world point it always was, so neighbouring
    // cells still meet and nothing can float or stick out.
    public static class CityMapBuilder
    {
        private const string ScenePath = "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity";
        private const string ModelPath = "Assets/CityModel/City.fbx";
        private const string MeshFolder = "Assets/GameAssets/Meshes/CityMap";

        public const string RootName = "CityMap";

        // Roughly forty metres a cell. Smaller cells cull better but cost a draw call per
        // material per cell, and this model carries seventy two materials.
        private const int Grid = 8;

        // Clear of the circuit and of every car park, so the two maps cannot see each other.
        private static readonly Vector3 Origin = new Vector3(2000f, 0f, 2000f);

        [MenuItem("Tools/Car Parking/Build City Free Drive Map")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError($"[CityMapBuilder] Could not open '{ScenePath}'.");
                return;
            }

            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;

            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);

            if (model == null)
            {
                Debug.LogError($"[CityMapBuilder] '{ModelPath}' is not imported.");
                return;
            }

            RemoveOld();
            Directory.CreateDirectory(MeshFolder);

            foreach (string old in Directory.GetFiles(MeshFolder, "*.asset"))
            {
                AssetDatabase.DeleteAsset(old.Replace('\\', '/'));
            }

            var root = new GameObject(RootName);
            root.transform.position = Origin;

            GameObject source = Object.Instantiate(model);
            source.transform.position = Vector3.zero;

            int chunks = 0;
            int submeshes = 0;
            long triangles = 0;

            foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                var renderer = filter.GetComponent<MeshRenderer>();
                Material[] materials = renderer != null ? renderer.sharedMaterials : new Material[0];

                Cut(filter, materials, root.transform, ref chunks, ref submeshes, ref triangles);
            }

            Object.DestroyImmediate(source);

            Transform spawn = PlaceSpawn(root.transform);

            // Off until the player picks this map. One free-drive map is loaded at a time,
            // and in Practice and Challenge neither is.
            root.SetActive(false);

            Wire(spawn);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[CityMapBuilder] Built the city: {chunks} chunk(s), {submeshes} draw call(s) if all "
                + $"were visible at once, {triangles} triangles. Spawn at {(spawn != null ? spawn.position.ToString("0.0") : "none")}.");
        }

        public static void BuildFromCommandLine()
        {
            Build();
            EditorApplication.Exit(0);
        }

        private static void RemoveOld()
        {
            foreach (GameObject found in Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (found != null && found.name == RootName && found.transform.parent == null)
                {
                    Object.DestroyImmediate(found);
                }
            }
        }

        // ----- cutting -------------------------------------------------------------------

        private static void Cut(MeshFilter filter, Material[] materials, Transform root,
            ref int chunks, ref int submeshes, ref long triangles)
        {
            Mesh mesh = filter.sharedMesh;
            Transform at = filter.transform;

            Vector3[] points = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = at.TransformPoint(points[i]);
            }

            var sourceTriangles = new List<int[]>();

            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                sourceTriangles.Add(mesh.GetTriangles(s));
            }

            Bounds bounds = mesh.bounds;
            Vector3 min = at.TransformPoint(bounds.min);
            Vector3 max = at.TransformPoint(bounds.max);

            float lowX = Mathf.Min(min.x, max.x);
            float lowZ = Mathf.Min(min.z, max.z);
            float cellX = Mathf.Max(1f, Mathf.Abs(max.x - min.x) / Grid);
            float cellZ = Mathf.Max(1f, Mathf.Abs(max.z - min.z) / Grid);

            for (int gx = 0; gx < Grid; gx++)
            {
                for (int gz = 0; gz < Grid; gz++)
                {
                    // The far edge of the last cell is open, so a triangle whose centroid
                    // lands exactly on the model's own boundary still belongs somewhere.
                    // With a closed bound the outermost strip of the map was dropped.
                    float highX = gx == Grid - 1 ? float.MaxValue : lowX + (gx + 1) * cellX;
                    float highZ = gz == Grid - 1 ? float.MaxValue : lowZ + (gz + 1) * cellZ;

                    GameObject chunk = CutCell(points, normals, uvs, sourceTriangles, materials, root,
                        lowX + gx * cellX, highX,
                        lowZ + gz * cellZ, highZ,
                        $"{mesh.name}_{gx}_{gz}", ref submeshes, ref triangles);

                    if (chunk != null)
                    {
                        chunks++;
                    }
                }
            }
        }

        private static GameObject CutCell(Vector3[] points, Vector3[] normals, Vector2[] uvs,
            List<int[]> sourceTriangles, Material[] materials, Transform root,
            float minX, float maxX, float minZ, float maxZ, string name,
            ref int submeshes, ref long triangles)
        {
            var remap = new Dictionary<int, int>();
            var vertices = new List<Vector3>();
            var newNormals = new List<Vector3>();
            var newUvs = new List<Vector2>();

            var kept = new List<List<int>>();
            var keptMaterials = new List<Material>();

            int total = 0;

            for (int s = 0; s < sourceTriangles.Count; s++)
            {
                int[] source = sourceTriangles[s];
                var mine = new List<int>();

                for (int t = 0; t < source.Length; t += 3)
                {
                    Vector3 a = points[source[t]];
                    Vector3 b = points[source[t + 1]];
                    Vector3 c = points[source[t + 2]];

                    float x = (a.x + b.x + c.x) / 3f;
                    float z = (a.z + b.z + c.z) / 3f;

                    if (x < minX || x >= maxX || z < minZ || z >= maxZ)
                    {
                        continue;
                    }

                    for (int k = 0; k < 3; k++)
                    {
                        int original = source[t + k];

                        if (!remap.TryGetValue(original, out int mapped))
                        {
                            mapped = vertices.Count;
                            remap[original] = mapped;

                            vertices.Add(points[original]);

                            if (normals.Length == points.Length)
                            {
                                newNormals.Add(normals[original]);
                            }

                            if (uvs.Length == points.Length)
                            {
                                newUvs.Add(uvs[original]);
                            }
                        }

                        mine.Add(mapped);
                    }

                    total++;
                }

                // An empty submesh is still a draw call. This model has seventy two
                // materials and no cell uses more than a handful of them, so dropping the
                // empty ones is most of what keeps the draw call count down.
                if (mine.Count > 0)
                {
                    kept.Add(mine);
                    keptMaterials.Add(s < materials.Length ? materials[s] : null);
                }
            }

            if (total == 0)
            {
                return null;
            }

            var chunkMesh = new Mesh { name = name };
            chunkMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            chunkMesh.SetVertices(vertices);

            if (newNormals.Count == vertices.Count)
            {
                chunkMesh.SetNormals(newNormals);
            }

            if (newUvs.Count == vertices.Count)
            {
                chunkMesh.SetUVs(0, newUvs);
            }

            chunkMesh.subMeshCount = kept.Count;

            for (int s = 0; s < kept.Count; s++)
            {
                chunkMesh.SetTriangles(kept[s], s);
            }

            chunkMesh.RecalculateBounds();

            if (newNormals.Count != vertices.Count)
            {
                chunkMesh.RecalculateNormals();
            }

            AssetDatabase.CreateAsset(chunkMesh, $"{MeshFolder}/{name}.asset");

            var chunk = new GameObject(name);
            chunk.transform.SetParent(root, false);
            chunk.transform.localPosition = Vector3.zero;

            chunk.AddComponent<MeshFilter>().sharedMesh = chunkMesh;
            chunk.AddComponent<MeshRenderer>().sharedMaterials = keptMaterials.ToArray();

            // What the car drives on and bumps into. Non-convex, which is only allowed for
            // something that never moves - and none of this ever moves.
            chunk.AddComponent<MeshCollider>().sharedMesh = chunkMesh;

            submeshes += kept.Count;
            triangles += total;

            return chunk;
        }

        // ----- spawn ---------------------------------------------------------------------

        // Somewhere the car can be put down: a street downtown, with road either side of it
        // and nothing overhead. Found by dropping rays rather than by picking a number,
        // because a spawn inside a building is a map the player cannot start.
        //
        // Aimed at the middle of the tall buildings, not the middle of the map. The middle
        // of this map is the river, and going by that put the car on a strip of embankment
        // with a wall on one side and water on the other.
        private static Transform PlaceSpawn(Transform root)
        {
            Physics.SyncTransforms();

            Bounds bounds = Measure(root);
            Vector2 downtown = Downtown(root, bounds);

            float best = float.MaxValue;
            Vector3 chosen = Vector3.zero;
            bool found = false;

            for (float x = bounds.min.x + 10f; x <= bounds.max.x - 10f; x += 3f)
            {
                for (float z = bounds.min.z + 10f; z <= bounds.max.z - 10f; z += 3f)
                {
                    if (!IsOpenGround(new Vector3(x, 0f, z), root, out Vector3 point))
                    {
                        continue;
                    }

                    // Road on every side, so the car is not boxed in by a wall it cannot
                    // reverse away from.
                    if (!HasRoomAllRound(point, root))
                    {
                        continue;
                    }

                    float distance = (new Vector2(point.x, point.z) - downtown).sqrMagnitude;

                    if (distance < best)
                    {
                        best = distance;
                        chosen = point;
                        found = true;
                    }
                }
            }

            if (!found)
            {
                Debug.LogError("[CityMapBuilder] Found nowhere in the city to put the car down.");
                return null;
            }

            var spawn = new GameObject("CitySpawn");
            spawn.transform.SetParent(root, true);
            spawn.transform.position = chosen + Vector3.up * 0.2f;

            // Pointed down the longest clear run from where it stands, which on a street is
            // the street. Facing a wall from a standing start reads as a broken spawn even
            // when there is room to turn.
            spawn.transform.rotation = Quaternion.LookRotation(LongestRun(chosen, root), Vector3.up);

            Debug.Log($"[CityMapBuilder] Spawn {chosen:0.0}, downtown centre {downtown:0.0}, "
                + $"{(new Vector2(chosen.x, chosen.z) - downtown).magnitude:0}m from it.");

            return spawn.transform;
        }

        // Where the tall buildings are, by averaging everything standing more than twenty
        // metres up. That is the part of the model worth starting the player in.
        private static Vector2 Downtown(Transform root, Bounds bounds)
        {
            double x = 0;
            double z = 0;
            long count = 0;

            float tall = root.position.y + 20f;

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                foreach (Vector3 point in filter.sharedMesh.vertices)
                {
                    Vector3 world = filter.transform.TransformPoint(point);

                    if (world.y < tall)
                    {
                        continue;
                    }

                    x += world.x;
                    z += world.z;
                    count++;
                }
            }

            return count > 0
                ? new Vector2((float)(x / count), (float)(z / count))
                : new Vector2(bounds.center.x, bounds.center.z);
        }

        private static bool HasRoomAllRound(Vector3 point, Transform root)
        {
            foreach (Vector3 direction in Compass)
            {
                if (ClearRun(point, direction, root) < 5f)
                {
                    return false;
                }
            }

            return true;
        }

        private static Vector3 LongestRun(Vector3 point, Transform root)
        {
            Vector3 best = Vector3.forward;
            float longest = -1f;

            foreach (Vector3 direction in Compass)
            {
                float run = ClearRun(point, direction, root);

                if (run > longest)
                {
                    longest = run;
                    best = direction;
                }
            }

            return best;
        }

        // How far a car could go this way before the ground stops being the ground.
        private static float ClearRun(Vector3 from, Vector3 direction, Transform root)
        {
            const float Step = 1.5f;
            const float Limit = 45f;

            for (float along = Step; along <= Limit; along += Step)
            {
                Vector3 at = from + direction * along;
                Vector3 above = new Vector3(at.x, root.position.y + 60f, at.z);

                if (!Physics.Raycast(above, Vector3.down, out RaycastHit hit, 200f)
                    || Mathf.Abs(hit.point.y - from.y) > 0.35f)
                {
                    return along - Step;
                }
            }

            return Limit;
        }

        private static readonly Vector3[] Compass =
        {
            Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
            new Vector3(0.7071f, 0f, 0.7071f), new Vector3(-0.7071f, 0f, 0.7071f),
            new Vector3(0.7071f, 0f, -0.7071f), new Vector3(-0.7071f, 0f, -0.7071f)
        };

        private static bool IsOpenGround(Vector3 column, Transform root, out Vector3 point)
        {
            point = Vector3.zero;

            Vector3 from = new Vector3(column.x, root.position.y + 60f, column.z);

            if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, 200f))
            {
                return false;
            }

            if (hit.collider.transform.root != root.root || !hit.collider.transform.IsChildOf(root))
            {
                return false;
            }

            // Street level, not a rooftop and not the water.
            if (Mathf.Abs(hit.point.y - root.position.y) > 0.6f)
            {
                return false;
            }

            point = hit.point;

            // Room for a car and nothing above it: a gap under a bridge deck or inside an
            // archway passes the ground test and still traps the player.
            if (Physics.Raycast(point + Vector3.up * 0.3f, Vector3.up, 8f))
            {
                return false;
            }

            // Flat all round, so the car is not set down against a kerb or on the lip of a
            // step it cannot climb off.
            foreach (Vector3 offset in Neighbours)
            {
                Vector3 beside = new Vector3(point.x + offset.x, root.position.y + 60f, point.z + offset.z);

                if (!Physics.Raycast(beside, Vector3.down, out RaycastHit near, 200f)
                    || Mathf.Abs(near.point.y - point.y) > 0.3f)
                {
                    return false;
                }
            }

            return true;
        }

        private static readonly Vector3[] Neighbours =
        {
            new Vector3(4f, 0f, 0f), new Vector3(-4f, 0f, 0f),
            new Vector3(0f, 0f, 4f), new Vector3(0f, 0f, -4f),
            new Vector3(3f, 0f, 3f), new Vector3(-3f, 0f, -3f)
        };

        private static Bounds Measure(Transform root)
        {
            Bounds bounds = default;
            bool first = true;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (first)
                {
                    bounds = renderer.bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

        // ----- wiring --------------------------------------------------------------------

        private static void Wire(Transform spawn)
        {
            var session = Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);

            if (session == null)
            {
                Debug.LogError("[CityMapBuilder] No GameSession to wire the city into.");
                return;
            }

            var serialized = new SerializedObject(session);

            SerializedProperty root = serialized.FindProperty("cityMapRoot");
            SerializedProperty point = serialized.FindProperty("cityRoamSpawn");

            if (root == null || point == null)
            {
                Debug.LogError("[CityMapBuilder] GameSession has no city fields; update the script first.");
                return;
            }

            root.objectReferenceValue = spawn != null ? spawn.root.gameObject : null;
            point.objectReferenceValue = spawn;
            serialized.ApplyModifiedProperties();
        }
    }
}
