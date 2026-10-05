using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CarParkingGame.EditorTools
{
    // The buildings that stand round a car park, generated rather than cut out of a model.
    //
    // The city model that was here before is one baked mesh. Cutting it into buildings
    // tears triangles: a wall is often a single triangle spanning several metres, so a cut
    // either keeps it - and it fans out of the chunk as a shard - or drops it and leaves a
    // hole. Both showed up in play, along with clumps of trees hanging in the air, which
    // were chunks whose lowest point was one of those shards reaching down below anything
    // that could be seen.
    //
    // A building generated from boxes cannot do any of that. It is also about three hundred
    // triangles instead of tens of thousands, which matters more here than detail does: the
    // player is inside a walled lot and never gets closer to one than the ring road.
    public static class CityBlockLibrary
    {
        private const string Folder = "Assets/GameAssets/Meshes/CityBlocks";
        private const string MaterialFolder = "Assets/GameAssets/Materials";

        private const float StoreyHeight = 3.2f;
        private const int Count = 16;

        // Submesh order, which every generated mesh keeps, so one material list fits them all.
        private const int Wall = 0;
        private const int Window = 1;
        private const int Trim = 2;

        // Flat pastels, which is what the rest of the game is painted in.
        private static readonly Color[] Palette =
        {
            new Color(0.93f, 0.66f, 0.67f),
            new Color(0.95f, 0.90f, 0.72f),
            new Color(0.72f, 0.86f, 0.78f),
            new Color(0.70f, 0.81f, 0.90f),
            new Color(0.89f, 0.79f, 0.63f),
            new Color(0.92f, 0.92f, 0.90f),
            new Color(0.85f, 0.57f, 0.45f),
            new Color(0.79f, 0.74f, 0.87f)
        };

        public sealed class Block
        {
            public Mesh mesh;
            public Material[] materials;
            public float width;
            public float depth;
            public float height;
        }

        [MenuItem("Tools/Car Parking/Generate City Blocks")]
        public static void Generate()
        {
            System.IO.Directory.CreateDirectory(Folder);

            foreach (string old in System.IO.Directory.GetFiles(Folder, "*.asset"))
            {
                AssetDatabase.DeleteAsset(old.Replace('\\', '/'));
            }

            // Fixed seed: a rebuild has to produce the same skyline, or every course in the
            // scene changes shape whenever any one of them is rebuilt.
            var random = new System.Random(8812);

            for (int i = 0; i < Count; i++)
            {
                Mesh mesh = BuildMesh(i, random);
                AssetDatabase.CreateAsset(mesh, $"{Folder}/{mesh.name}.asset");
            }

            WallMaterials();
            WindowMaterial();
            TrimMaterial();

            AssetDatabase.SaveAssets();
            Debug.Log($"[CityBlockLibrary] Generated {Count} building(s).");
        }

        public static void GenerateFromCommandLine()
        {
            Generate();
            EditorApplication.Exit(0);
        }

        public static List<Block> Load()
        {
            if (!AssetDatabase.IsValidFolder(Folder)
                || AssetDatabase.FindAssets("t:Mesh", new[] { Folder }).Length == 0)
            {
                Generate();
            }

            Material[] walls = WallMaterials();
            Material window = WindowMaterial();
            Material trim = TrimMaterial();

            var blocks = new List<Block>();

            for (int i = 0; i < Count; i++)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{Folder}/Block{i:00}.asset");

                if (mesh == null)
                {
                    continue;
                }

                Vector3 size = mesh.bounds.size;

                blocks.Add(new Block
                {
                    mesh = mesh,
                    materials = new[] { walls[i % walls.Length], window, trim },
                    width = size.x,
                    depth = size.z,
                    height = size.y
                });
            }

            return blocks;
        }

        // ----- geometry ------------------------------------------------------------------

        // Origin at the centre of the base and +Z the front, so a caller sets a building
        // down on the pavement line and turns it to face the road without having to know
        // anything about how it was made.
        private static Mesh BuildMesh(int index, System.Random random)
        {
            float width = 7f + (float)random.NextDouble() * 6f;
            float depth = 7f + (float)random.NextDouble() * 5f;
            int storeys = 2 + random.Next(7);
            float height = storeys * StoreyHeight;

            var mesh = new MeshBuilder(3);

            mesh.Box(new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), Wall);

            // A plinth and a cornice. Without them a building is a bare box, and a row of
            // bare boxes reads as packing crates rather than as a street.
            mesh.Box(new Vector3(0f, 0.25f, 0f), new Vector3(width + 0.5f, 0.5f, depth + 0.5f), Trim);
            mesh.Box(new Vector3(0f, height + 0.2f, 0f), new Vector3(width + 0.6f, 0.4f, depth + 0.6f), Trim);

            for (int storey = 0; storey < storeys; storey++)
            {
                // Ground floor is a shopfront: taller glass, less wall either side of it.
                bool ground = storey == 0;
                float tall = ground ? 2.1f : 1.5f;
                float inset = ground ? 0.9f : 1.5f;
                float y = ground ? 1.35f : storey * StoreyHeight + StoreyHeight * 0.55f;

                // Two slabs crossing inside the building, each standing a few centimetres
                // proud of one pair of faces. Where they cross is buried in the wall box, so
                // what shows is a band of windows on all four sides.
                mesh.Box(new Vector3(0f, y, 0f), new Vector3(width - inset * 2f, tall, depth + 0.12f), Window);
                mesh.Box(new Vector3(0f, y, 0f), new Vector3(width + 0.12f, tall, depth - inset * 2f), Window);

                if (!ground)
                {
                    mesh.Box(new Vector3(0f, storey * StoreyHeight, 0f),
                        new Vector3(width + 0.3f, 0.2f, depth + 0.3f), Trim);
                }
            }

            // Something on the roof on about half of them, so the skyline is not a row of
            // flat tops at eight different heights.
            if (index % 2 == 0)
            {
                mesh.Box(new Vector3(0f, height + 0.4f + 1.1f, 0f),
                    new Vector3(width * 0.35f, 2.2f, depth * 0.35f), Wall);
            }

            return mesh.ToMesh($"Block{index:00}");
        }

        private sealed class MeshBuilder
        {
            private static readonly Vector3[] FaceNormals =
            {
                Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
            };

            // Across and up for each face, chosen so that across cross up is the face
            // normal. That is what makes one winding order correct for all six.
            private static readonly Vector3[] FaceAcross =
            {
                Vector3.back, Vector3.forward, Vector3.right, Vector3.right, Vector3.right, Vector3.left
            };

            private static readonly Vector3[] FaceUp =
            {
                Vector3.up, Vector3.up, Vector3.back, Vector3.forward, Vector3.up, Vector3.up
            };

            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<int>[] triangles;

            public MeshBuilder(int submeshes)
            {
                triangles = new List<int>[submeshes];

                for (int i = 0; i < submeshes; i++)
                {
                    triangles[i] = new List<int>();
                }
            }

            public void Box(Vector3 centre, Vector3 size, int submesh)
            {
                Vector3 half = size * 0.5f;

                for (int face = 0; face < 6; face++)
                {
                    Vector3 outward = Vector3.Scale(FaceNormals[face], half);
                    Vector3 across = Vector3.Scale(FaceAcross[face], half);
                    Vector3 up = Vector3.Scale(FaceUp[face], half);

                    int first = vertices.Count;

                    Add(centre + outward - across - up, FaceNormals[face], new Vector2(0f, 0f));
                    Add(centre + outward + across - up, FaceNormals[face], new Vector2(1f, 0f));
                    Add(centre + outward + across + up, FaceNormals[face], new Vector2(1f, 1f));
                    Add(centre + outward - across + up, FaceNormals[face], new Vector2(0f, 1f));

                    triangles[submesh].Add(first);
                    triangles[submesh].Add(first + 1);
                    triangles[submesh].Add(first + 2);
                    triangles[submesh].Add(first);
                    triangles[submesh].Add(first + 2);
                    triangles[submesh].Add(first + 3);
                }
            }

            private void Add(Vector3 position, Vector3 normal, Vector2 uv)
            {
                vertices.Add(position);
                normals.Add(normal);
                uvs.Add(uv);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };

                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = triangles.Length;

                for (int i = 0; i < triangles.Length; i++)
                {
                    mesh.SetTriangles(triangles[i], i);
                }

                mesh.RecalculateBounds();
                return mesh;
            }
        }

        // ----- materials -----------------------------------------------------------------

        private static Material[] WallMaterials()
        {
            var materials = new Material[Palette.Length];

            for (int i = 0; i < Palette.Length; i++)
            {
                materials[i] = Paint($"CityWall{i}", Palette[i], 0.05f);
            }

            return materials;
        }

        private static Material WindowMaterial()
        {
            // Dark and a little shiny, which is the only thing on these buildings that
            // catches the light and keeps them from reading as cardboard.
            return Paint("CityWindow", new Color(0.17f, 0.21f, 0.28f), 0.6f);
        }

        private static Material TrimMaterial()
        {
            return Paint("CityTrim", new Color(0.88f, 0.88f, 0.86f), 0.05f);
        }

        private static Material Paint(string name, Color color, float smoothness)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader);
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/GameAssets", "Materials");
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
