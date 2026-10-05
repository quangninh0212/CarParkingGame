using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CarParkingGame.EditorTools
{
    // Cuts the city model into single buildings and keeps them as mesh assets.
    //
    // The model is one mesh of a million triangles. Nothing can be culled from a single
    // mesh, so one corner of it on screen costs the whole million every frame. Cut into
    // buildings, a lot only pays for the handful standing round it.
    //
    // The cut is by triangle centroid, so each triangle lands in exactly one cell and no
    // geometry is duplicated or lost at a seam. Everything below a cell's own ground is
    // thrown away: the model's ground reaches far past the buildings standing on it, and
    // set round a car park it lies across the tarmac as a pale slab.
    public static class CityChunkLibrary
    {
        private const string Folder = "Assets/GameAssets/Meshes/CityChunks";

        // Fifteen metres a cell, which is about one building. Bigger cells gave blocks
        // that could not be packed against each other without overlapping.
        private const int GridSize = 20;

        private const float GroundStrip = 2.5f;
        private const float MinimumHeight = 8f;
        private const int MinimumTriangles = 400;

        // A chunk much wider than its own cell spans the city rather than stands in it -
        // a bridge deck, a long roof - and one of those laid beside a lot reaches across
        // it as a wedge filling half the screen.
        private const float MaximumCellShare = 1.3f;

        public sealed class Building
        {
            public Mesh mesh;
            public Material[] materials;
            public Vector3 size;
            public int triangles;
        }

        [MenuItem("Tools/Car Parking/Cut City Into Buildings")]
        public static void Cut()
        {
            if (!TryFindCity(out GameObject city, out string path))
            {
                return;
            }

            Directory.CreateDirectory(Folder);

            foreach (string old in Directory.GetFiles(Folder, "*.asset"))
            {
                AssetDatabase.DeleteAsset(old.Replace('\\', '/'));
            }

            GameObject instance = Object.Instantiate(city);
            var kept = new List<Building>();

            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                var renderer = filter.GetComponent<MeshRenderer>();

                CutMesh(filter.sharedMesh, filter.transform,
                    renderer != null ? renderer.sharedMaterials : new Material[0], kept);
            }

            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();

            Debug.Log($"[CityChunkLibrary] Cut '{path}' into {kept.Count} building(s).");
        }

        public static void CutFromCommandLine()
        {
            Cut();
            EditorApplication.Exit(0);
        }

        // Everything cut earlier, tallest first, so a caller takes buildings rather than
        // sheds without having to know how they were made.
        public static List<Building> Load()
        {
            var buildings = new List<Building>();

            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);

                if (mesh == null)
                {
                    continue;
                }

                buildings.Add(new Building
                {
                    mesh = mesh,
                    materials = CityMaterials(mesh.subMeshCount),
                    size = mesh.bounds.size,
                    triangles = mesh.triangles.Length / 3
                });
            }

            // Tallest first, and only the top of that list is handed out.
            //
            // The cut takes everything in the model that stands up, and most of what
            // stands up in a city model is trees. Picking at random from all of it ringed
            // every car park with a forest. Buildings are the tall things.
            buildings.Sort((a, b) => b.size.y.CompareTo(a.size.y));

            const int Tallest = 18;

            if (buildings.Count > Tallest)
            {
                buildings.RemoveRange(Tallest, buildings.Count - Tallest);
            }

            return buildings;
        }

        // The chunks keep the model's own submesh order, so the model's own material list
        // fits them all.
        private static Material[] CityMaterials(int count)
        {
            if (!TryFindCity(out GameObject city, out _))
            {
                return new Material[count];
            }

            // The longest list in the model, not the first.
            //
            // The city is two meshes: a big one carrying forty one materials and a small
            // one carrying a handful. Taking the first renderer found handed every chunk
            // the short list, so most of their submeshes had no material at all and the
            // whole skyline rendered magenta.
            Material[] source = new Material[0];

            foreach (MeshRenderer renderer in city.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.sharedMaterials.Length > source.Length)
                {
                    source = renderer.sharedMaterials;
                }
            }

            var materials = new Material[count];

            for (int i = 0; i < count; i++)
            {
                materials[i] = i < source.Length ? source[i] : null;
            }

            return materials;
        }

        private static bool TryFindCity(out GameObject city, out string path)
        {
            city = null;
            path = null;

            long best = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets" }))
            {
                string candidate = AssetDatabase.GUIDToAssetPath(guid);

                // The game's own art lives under GameAssets; a city dropped in by hand
                // does not. Picking the largest loose model finds it without needing a
                // filename that has Cyrillic in it.
                if (candidate.StartsWith("Assets/GameAssets/") || candidate.StartsWith("Assets/Prototype"))
                {
                    continue;
                }

                var info = new FileInfo(candidate);

                if (info.Exists && info.Length > best)
                {
                    best = info.Length;
                    path = candidate;
                }
            }

            if (path == null)
            {
                Debug.LogError("[CityChunkLibrary] No city model found outside GameAssets.");
                return false;
            }

            city = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return city != null;
        }

        // ----- cutting -------------------------------------------------------------------

        private static void CutMesh(Mesh source, Transform at, Material[] materials, List<Building> kept)
        {
            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            Vector2[] uvs = source.uv;

            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = at.TransformPoint(vertices[i]);
            }

            Vector3 min = at.TransformPoint(source.bounds.min);
            Vector3 max = at.TransformPoint(source.bounds.max);

            float cellX = Mathf.Max(1f, Mathf.Abs(max.x - min.x) / GridSize);
            float cellZ = Mathf.Max(1f, Mathf.Abs(max.z - min.z) / GridSize);

            var submeshes = new List<int[]>();

            for (int s = 0; s < source.subMeshCount; s++)
            {
                submeshes.Add(source.GetTriangles(s));
            }

            float lowX = Mathf.Min(min.x, max.x);
            float lowZ = Mathf.Min(min.z, max.z);

            for (int gx = 0; gx < GridSize; gx++)
            {
                for (int gz = 0; gz < GridSize; gz++)
                {
                    Building building = CutCell(vertices, normals, uvs, submeshes, materials,
                        lowX + gx * cellX, lowX + (gx + 1) * cellX,
                        lowZ + gz * cellZ, lowZ + (gz + 1) * cellZ,
                        Mathf.Max(cellX, cellZ), kept.Count);

                    if (building != null)
                    {
                        kept.Add(building);
                    }
                }
            }
        }

        private static Building CutCell(Vector3[] vertices, Vector3[] normals, Vector2[] uvs,
            List<int[]> submeshes, Material[] materials,
            float minX, float maxX, float minZ, float maxZ, float cell, int index)
        {
            // Two passes: where the ground of this cell is cannot be known until every
            // triangle in it has been seen, and nothing can be judged "up in the air"
            // before that.
            float ground = float.MaxValue;

            foreach (int[] triangles in submeshes)
            {
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    Vector3 a = vertices[triangles[t]];
                    Vector3 b = vertices[triangles[t + 1]];
                    Vector3 c = vertices[triangles[t + 2]];

                    if (!Inside(a, b, c, minX, maxX, minZ, maxZ))
                    {
                        continue;
                    }

                    ground = Mathf.Min(ground, a.y, b.y, c.y);
                }
            }

            if (ground == float.MaxValue)
            {
                return null;
            }

            var remap = new Dictionary<int, int>();
            var newVertices = new List<Vector3>();
            var newNormals = new List<Vector3>();
            var newUvs = new List<Vector2>();
            var newSubmeshes = new List<List<int>>();

            int total = 0;

            foreach (int[] triangles in submeshes)
            {
                var keptTriangles = new List<int>();

                for (int t = 0; t < triangles.Length; t += 3)
                {
                    Vector3 a = vertices[triangles[t]];
                    Vector3 b = vertices[triangles[t + 1]];
                    Vector3 c = vertices[triangles[t + 2]];

                    if (!Inside(a, b, c, minX, maxX, minZ, maxZ))
                    {
                        continue;
                    }

                    if ((a.y + b.y + c.y) / 3f < ground + GroundStrip)
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

                        keptTriangles.Add(mapped);
                    }

                    total++;
                }

                newSubmeshes.Add(keptTriangles);
            }

            if (total < MinimumTriangles)
            {
                return null;
            }

            var mesh = new Mesh { name = $"Building{index:000}" };
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

            if (newNormals.Count != newVertices.Count)
            {
                mesh.RecalculateNormals();
            }

            Vector3 size = mesh.bounds.size;

            if (size.y < MinimumHeight
                || size.x > cell * MaximumCellShare
                || size.z > cell * MaximumCellShare)
            {
                Object.DestroyImmediate(mesh);
                return null;
            }

            // A building is roughly as deep as it is wide. Something long and narrow is a
            // span rather than a block - a stretch of bridge deck, a run of pylons - and
            // laid beside a car park one of those reads as wreckage rather than as a
            // street. They are tall, so height alone lets them through.
            float broad = Mathf.Max(size.x, size.z);
            float narrow = Mathf.Max(0.01f, Mathf.Min(size.x, size.z));

            if (broad / narrow > 2.2f || narrow < 4f)
            {
                Object.DestroyImmediate(mesh);
                return null;
            }

            // Recentred on its own base, so a caller can set a building down at a point and
            // have it stand there rather than having to know where in the city it came from.
            Vector3[] moved = mesh.vertices;
            Vector3 offset = new Vector3(mesh.bounds.center.x, mesh.bounds.min.y, mesh.bounds.center.z);

            for (int i = 0; i < moved.Length; i++)
            {
                moved[i] -= offset;
            }

            mesh.vertices = moved;
            mesh.RecalculateBounds();

            AssetDatabase.CreateAsset(mesh, $"{Folder}/{mesh.name}.asset");

            return new Building { mesh = mesh, materials = materials, size = size, triangles = total };
        }

        private static bool Inside(Vector3 a, Vector3 b, Vector3 c,
            float minX, float maxX, float minZ, float maxZ)
        {
            float x = (a.x + b.x + c.x) / 3f;
            float z = (a.z + b.z + c.z) / 3f;

            return x >= minX && x < maxX && z >= minZ && z < maxZ;
        }
    }
}
