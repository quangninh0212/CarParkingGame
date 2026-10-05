using System.Collections.Generic;
using System.IO;
using CarParkingGame.Missions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarParkingGame.EditorTools
{
    // Rings the training site with a city skyline.
    //
    // The city model is one mesh of a million triangles. Placed whole, nothing can be
    // culled: one corner of it on screen costs the whole million, every frame, on a phone.
    // So it is cut into a grid of chunks first, the chunks that are mostly building are
    // kept, and those are set around the site. Culling then throws away everything the
    // player is not looking at, and from inside a walled lot that is nearly all of it.
    //
    // The player never drives out there - the lots are sealed - so this is a backdrop.
    // That is what lets it be cut up and rearranged without regard for whether the streets
    // in it join.
    public static class CitySurroundBuilder
    {
        private const string ScenePath = "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity";
        private const string ChunkFolder = "Assets/GameAssets/Meshes/CityChunks";
        private const string RootName = "CitySurround";

        private const int FirstSiteMission = 9;

        // How finely the city is cut. Twelve across a three hundred metre model is about
        // twenty-five metres a chunk, which is a block of buildings.
        private const int GridSize = 12;

        // A chunk has to be this tall to count as buildings rather than road, river or
        // field, and carry this many triangles to be worth placing at all.
        private const float BuildingHeight = 9f;
        private const int MinimumTriangles = 900;

        // How much of a chunk has to stand clear of its own base to count as buildings.
        //
        // Height alone is not enough: a cell holding one tower and a wide sheet of ground
        // or water is tall, and placed round the site it reads as a broken slab floating at
        // the horizon rather than as a city. Buildings carry most of their triangles up in
        // the air; a sheet of terrain carries nearly none.
        // Everything below this much above a chunk's own ground is thrown away.
        //
        // A chunk carries the city ground its buildings stand on, and that ground reaches
        // well past them. Set round a car park it lay across the tarmac as a pale slab.
        // The buildings alone are what the backdrop needs; they are stood on the site's
        // own ground instead.
        private const float GroundStrip = 2.5f;

        private const float AboveBase = 3f;
        private const float MinimumAboveShare = 0.35f;

        // Chunks are placed this far out from the edge of the site, and this far apart.
        private const float StandOff = 54f;
        private const float Spacing = 26f;

        [MenuItem("Tools/Car Parking/Build City Surround")]
        public static void RunInOpenScene()
        {
            Build();
            Debug.Log("[CitySurroundBuilder] Done. Save the scene to keep it.");
        }

        public static void RunFromCommandLine()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError($"[CitySurroundBuilder] Could not open '{ScenePath}'.");
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
            if (!TryFindCity(out GameObject city))
            {
                return false;
            }

            if (!TryMeasureSite(out Bounds site))
            {
                return false;
            }

            List<Chunk> chunks = Cut(city);

            if (chunks.Count == 0)
            {
                Debug.LogError("[CitySurroundBuilder] The model produced no chunks tall enough to be buildings.");
                return false;
            }

            Place(chunks, site);
            return true;
        }

        // ----- finding the model ---------------------------------------------------------

        private static bool TryFindCity(out GameObject city)
        {
            city = null;

            string best = null;
            long bestSize = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // The game's own art lives in GameAssets; a city dropped in by hand does
                // not. Picking the largest loose model is how this finds it without being
                // told a filename that has Cyrillic in it.
                if (path.StartsWith("Assets/GameAssets/") || path.StartsWith("Assets/Prototype"))
                {
                    continue;
                }

                var info = new FileInfo(path);

                if (info.Exists && info.Length > bestSize)
                {
                    bestSize = info.Length;
                    best = path;
                }
            }

            if (best == null)
            {
                Debug.LogError("[CitySurroundBuilder] No city model found outside GameAssets.");
                return false;
            }

            city = AssetDatabase.LoadAssetAtPath<GameObject>(best);

            if (city == null)
            {
                Debug.LogError($"[CitySurroundBuilder] '{best}' did not import as a model.");
                return false;
            }

            Debug.Log($"[CitySurroundBuilder] Using '{best}' ({bestSize / 1048576}MB).");
            return true;
        }

        private static bool TryMeasureSite(out Bounds site)
        {
            site = default;

            var manager = Object.FindFirstObjectByType<MissionManager>(FindObjectsInactive.Include);

            if (manager == null)
            {
                Debug.LogError("[CitySurroundBuilder] No MissionManager in the scene.");
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

                bool wasActive = mission.gameObject.activeSelf;
                mission.SetEnvironmentActive(true);

                foreach (Renderer renderer in mission.GetComponentsInChildren<Renderer>(true))
                {
                    if (!any)
                    {
                        site = renderer.bounds;
                        any = true;
                    }
                    else
                    {
                        site.Encapsulate(renderer.bounds);
                    }
                }

                mission.gameObject.SetActive(wasActive);
            }

            if (!any)
            {
                Debug.LogError("[CitySurroundBuilder] Could not measure the training site.");
            }

            return any;
        }

        // ----- cutting the city ----------------------------------------------------------

        private sealed class Chunk
        {
            public Mesh mesh;
            public Material[] materials;
            public float height;
            public int triangles;
        }

        private static List<Chunk> Cut(GameObject city)
        {
            Directory.CreateDirectory(ChunkFolder);

            foreach (string old in Directory.GetFiles(ChunkFolder, "*.asset"))
            {
                AssetDatabase.DeleteAsset(old.Replace('\\', '/'));
            }

            GameObject instance = Object.Instantiate(city);
            var chunks = new List<Chunk>();

            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                var renderer = filter.GetComponent<MeshRenderer>();
                Material[] materials = renderer != null ? renderer.sharedMaterials : new Material[0];

                CutMesh(filter.sharedMesh, filter.transform, materials, chunks);
            }

            Object.DestroyImmediate(instance);

            // Tallest first, and only the top of that list is used.
            //
            // Sorting by triangle count picked the bridge and the wooded bank as often as
            // a city block, because a lattice of girders and a hillside of trees are dense
            // without being tall. What has to show over the wall of a car park is height.
            chunks.Sort((a, b) => b.height.CompareTo(a.height));

            const int Tallest = 20;

            if (chunks.Count > Tallest)
            {
                chunks.RemoveRange(Tallest, chunks.Count - Tallest);
            }

            Debug.Log($"[CitySurroundBuilder] Kept the {chunks.Count} tallest chunk(s), " +
                      $"{chunks[0].height:0}m down to {chunks[chunks.Count - 1].height:0}m.");
            return chunks;
        }

        private static void CutMesh(Mesh source, Transform at, Material[] materials, List<Chunk> chunks)
        {
            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            Vector2[] uvs = source.uv;

            // Everything is baked into the model's own space so a chunk can be moved and
            // turned on its own afterwards.
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = at.TransformPoint(vertices[i]);
            }

            Bounds bounds = source.bounds;
            Vector3 min = at.TransformPoint(bounds.min);
            Vector3 max = at.TransformPoint(bounds.max);

            float cellX = Mathf.Max(1f, (max.x - min.x) / GridSize);
            float cellZ = Mathf.Max(1f, (max.z - min.z) / GridSize);

            var submeshes = new List<int[]>();

            for (int s = 0; s < source.subMeshCount; s++)
            {
                submeshes.Add(source.GetTriangles(s));
            }

            for (int gx = 0; gx < GridSize; gx++)
            {
                for (int gz = 0; gz < GridSize; gz++)
                {
                    float lowX = Mathf.Min(min.x, max.x) + gx * cellX;
                    float lowZ = Mathf.Min(min.z, max.z) + gz * cellZ;

                    Chunk chunk = CutCell(vertices, normals, uvs, submeshes, materials,
                        lowX, lowX + cellX, lowZ, lowZ + cellZ, chunks.Count);

                    if (chunk != null)
                    {
                        chunks.Add(chunk);
                    }
                }
            }
        }

        private static Chunk CutCell(Vector3[] vertices, Vector3[] normals, Vector2[] uvs,
            List<int[]> submeshes, Material[] materials,
            float minX, float maxX, float minZ, float maxZ, int index)
        {
            var remap = new Dictionary<int, int>();
            var newVertices = new List<Vector3>();
            var newNormals = new List<Vector3>();
            var newUvs = new List<Vector2>();
            var newSubmeshes = new List<List<int>>();

            int total = 0;
            int aloft = 0;
            float low = float.MaxValue;
            float high = float.MinValue;

            // Two passes over the cell: the first only to find where its ground is, since
            // whether a triangle is "up in the air" cannot be judged until that is known.
            foreach (int[] scan in submeshes)
            {
                for (int t = 0; t < scan.Length; t += 3)
                {
                    Vector3 a = vertices[scan[t]];
                    Vector3 b = vertices[scan[t + 1]];
                    Vector3 c = vertices[scan[t + 2]];

                    float cx = (a.x + b.x + c.x) / 3f;
                    float cz = (a.z + b.z + c.z) / 3f;

                    if (cx < minX || cx >= maxX || cz < minZ || cz >= maxZ)
                    {
                        continue;
                    }

                    low = Mathf.Min(low, a.y, b.y, c.y);
                }
            }

            float groundLevel = low;
            low = float.MaxValue;

            foreach (int[] triangles in submeshes)
            {
                var kept = new List<int>();

                for (int t = 0; t < triangles.Length; t += 3)
                {
                    Vector3 a = vertices[triangles[t]];
                    Vector3 b = vertices[triangles[t + 1]];
                    Vector3 c = vertices[triangles[t + 2]];

                    // By centroid, so a triangle lands in exactly one cell and no geometry
                    // is duplicated or dropped at a seam.
                    float cx = (a.x + b.x + c.x) / 3f;
                    float cz = (a.z + b.z + c.z) / 3f;

                    if (cx < minX || cx >= maxX || cz < minZ || cz >= maxZ)
                    {
                        continue;
                    }

                    if ((a.y + b.y + c.y) / 3f < groundLevel + GroundStrip)
                    {
                        continue;
                    }

                    for (int k = 0; k < 3; k++)
                    {
                        int original = triangles[t + k];

                        if (!remap.TryGetValue(original, out int mapped))
                        {
                            mapped = newVertices.Count;
                            remap[original] = mapped;

                            newVertices.Add(vertices[original]);

                            if (normals.Length == vertices.Length)
                            {
                                newNormals.Add(normals[original]);
                            }

                            if (uvs.Length == vertices.Length)
                            {
                                newUvs.Add(uvs[original]);
                            }
                        }

                        kept.Add(mapped);
                    }

                    low = Mathf.Min(low, a.y, b.y, c.y);
                    high = Mathf.Max(high, a.y, b.y, c.y);
                    total++;

                    if ((a.y + b.y + c.y) / 3f > groundLevel + AboveBase)
                    {
                        aloft++;
                    }
                }

                newSubmeshes.Add(kept);
            }

            float height = high - low;

            if (total < MinimumTriangles || height < BuildingHeight)
            {
                return null;
            }

            if (aloft < total * MinimumAboveShare)
            {
                return null;
            }

            var mesh = new Mesh { name = $"CityChunk{index:000}" };

            // A city block runs past sixty five thousand vertices without trying.
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(newVertices);

            if (newNormals.Count == newVertices.Count)
            {
                mesh.SetNormals(newNormals);
            }

            if (newUvs.Count == newVertices.Count)
            {
                mesh.SetUVs(0, newUvs);
            }

            mesh.subMeshCount = newSubmeshes.Count;

            for (int s = 0; s < newSubmeshes.Count; s++)
            {
                mesh.SetTriangles(newSubmeshes[s], s);
            }

            mesh.RecalculateBounds();

            // A chunk wider than its own cell is a thing that spans the city rather than
            // stands in it - a bridge deck, a long roof. Set round a car park, one of those
            // reaches across the lot as a cream wedge filling half the screen. Cut by
            // centroid the geometry is kept whole, which is right, but it means a few
            // chunks come out enormous and those are no use as a backdrop.
            Vector3 footprint = mesh.bounds.size;
            float cell = Mathf.Max(maxX - minX, maxZ - minZ);

            if (footprint.x > cell * 1.6f || footprint.z > cell * 1.6f)
            {
                Object.DestroyImmediate(mesh);
                return null;
            }


            if (newNormals.Count != newVertices.Count)
            {
                mesh.RecalculateNormals();
            }

            AssetDatabase.CreateAsset(mesh, $"{ChunkFolder}/{mesh.name}.asset");

            return new Chunk { mesh = mesh, materials = materials, height = height, triangles = total };
        }

        // ----- placing the ring -----------------------------------------------------------

        private static void Place(List<Chunk> chunks, Bounds site)
        {
            GameObject old = GameObject.Find(RootName);

            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build city surround");

            // Repeatable: the same seed lays the same skyline on every rebuild, so the
            // scene diff is the geometry changing and not the dice.
            var random = new System.Random(20261006);

            float left = site.min.x - StandOff;
            float right = site.max.x + StandOff;
            float near = site.min.z - StandOff;
            float far = site.max.z + StandOff;
            float y = site.min.y;

            int placed = 0;
            long triangles = 0;

            placed += Run(root.transform, chunks, random, new Vector3(left, y, near), new Vector3(right, y, near), ref triangles);
            placed += Run(root.transform, chunks, random, new Vector3(left, y, far), new Vector3(right, y, far), ref triangles);
            placed += Run(root.transform, chunks, random, new Vector3(left, y, near), new Vector3(left, y, far), ref triangles);
            placed += Run(root.transform, chunks, random, new Vector3(right, y, near), new Vector3(right, y, far), ref triangles);

            Debug.Log($"[CitySurroundBuilder] {placed} block(s) round the site, {triangles:N0} triangles in all. " +
                      "Only the few in shot are drawn, because they are separate meshes.");
        }

        private static int Run(Transform parent, List<Chunk> chunks, System.Random random,
            Vector3 from, Vector3 to, ref long triangles)
        {
            float run = Vector3.Distance(from, to);
            int count = Mathf.Max(1, Mathf.FloorToInt(run / Spacing));
            int placed = 0;

            for (int i = 0; i <= count; i++)
            {
                Chunk chunk = chunks[random.Next(chunks.Count)];
                Vector3 at = Vector3.Lerp(from, to, i / (float)count);

                var block = new GameObject($"Block {parent.childCount:000}");
                block.transform.SetParent(parent, false);

                // Set down by its own base, so a block sits on the ground rather than
                // halfway into it or hovering over it.
                Bounds local = chunk.mesh.bounds;
                block.transform.position = at - new Vector3(local.center.x, local.min.y, local.center.z);
                block.transform.rotation = Quaternion.Euler(0f, random.Next(4) * 90f, 0f);

                block.AddComponent<MeshFilter>().sharedMesh = chunk.mesh;

                var renderer = block.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = chunk.materials;

                // Backdrop. Shadows from a skyline the player cannot reach are a cost with
                // nothing to show for it.
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                triangles += chunk.triangles;
                placed++;
            }

            return placed;
        }
    }
}
