using System.IO;
using UnityEditor;
using UnityEngine;

namespace CarParkingGame.EditorTools
{
    // Builds the game's own effects: two textures, the materials that use them, and the
    // prefabs the car and the mission runner fire off.
    //
    // The project ships with an effects pack, and the first attempt used it. Its effects
    // are driven by a script that only runs in play mode, so nothing I could do from the
    // editor would render one - which meant no way to tell a working effect from one that
    // silently shows nothing until the game is in front of someone. These are plain
    // ParticleSystems with no script on them, so they can be simulated, photographed and
    // judged before anyone sees them.
    public static class VfxLibraryGenerator
    {
        private const string TextureFolder = "Assets/GameAssets/Sprites/Vfx";
        private const string MaterialFolder = "Assets/GameAssets/Materials";
        private const string PrefabFolder = "Assets/GameAssets/Prefabs/Vfx";

        public const string PuffTexturePath = TextureFolder + "/SmokePuff.png";
        public const string DotTexturePath = TextureFolder + "/SparkDot.png";

        public const string SmokeMaterialPath = MaterialFolder + "/VfxSmoke.mat";
        public const string SparkMaterialPath = MaterialFolder + "/VfxSpark.mat";
        public const string SkidMaterialPath = MaterialFolder + "/VfxSkid.mat";

        public const string SparkPrefabPath = PrefabFolder + "/ImpactSparks.prefab";
        public const string BayPrefabPath = PrefabFolder + "/BayConfetti.prefab";
        public const string FinishPrefabPath = PrefabFolder + "/FinishFirework.prefab";

        [MenuItem("Tools/Car Parking/Generate VFX Library")]
        public static void Generate()
        {
            Directory.CreateDirectory(TextureFolder);
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(PrefabFolder);

            WriteTexture(PuffTexturePath, Puff(128));
            WriteTexture(DotTexturePath, Dot(64));

            AssetDatabase.Refresh();

            Import(PuffTexturePath);
            Import(DotTexturePath);

            Material smoke = Material(SmokeMaterialPath, PuffTexturePath, false);
            Material spark = Material(SparkMaterialPath, DotTexturePath, true);
            Material(SkidMaterialPath, null, false);

            BuildSparks(spark);
            BuildConfetti(spark);
            BuildFirework(spark);

            AssetDatabase.SaveAssets();

            Debug.Log($"[VfxLibraryGenerator] Wrote two textures, three materials and three effect prefabs under '{PrefabFolder}'. Smoke uses '{smoke.name}'.");
        }

        public static void GenerateFromCommandLine()
        {
            Generate();
            EditorApplication.Exit(0);
        }

        // ----- textures ---------------------------------------------------------------

        // A soft round puff. Without one, a particle is a flat square and smoke looks like
        // a stack of grey boxes - which is exactly how it looked.
        private static Color[] Puff(int size)
        {
            var pixels = new Color[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;

                    // Squared falloff: a linear one leaves a visible disc edge.
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha *= alpha;

                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            return pixels;
        }

        // A small hard dot with a halo, for sparks and confetti.
        private static Color[] Dot(int size)
        {
            var pixels = new Color[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;

                    float core = Mathf.Clamp01(1f - distance * 2.2f);
                    float halo = Mathf.Clamp01(1f - distance);

                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(core + halo * halo * 0.5f));
                }
            }

            return pixels;
        }

        private static void WriteTexture(string path, Color[] pixels)
        {
            int size = Mathf.RoundToInt(Mathf.Sqrt(pixels.Length));
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

            texture.SetPixels(pixels);
            texture.Apply();

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static void Import(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        // ----- materials --------------------------------------------------------------

        private static Material Material(string path, string texturePath, bool additive)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Sprites/Default");

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.mainTexture = texturePath == null
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            material.SetColor("_BaseColor", Color.white);

            // Transparent surface. Additive for anything that is light - a spark is
            // brighter than what is behind it, smoke is not.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 1f : 0f);
            material.renderQueue = 3000;

            EditorUtility.SetDirty(material);
            return material;
        }

        // ----- effects ----------------------------------------------------------------

        private static void BuildSparks(Material material)
        {
            var host = new GameObject("ImpactSparks");
            ParticleSystem system = Burst(host, material, 26, 0.55f, 4.5f, 0.1f,
                new Color(1f, 0.78f, 0.25f), 2.2f);

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 42f;
            shape.radius = 0.05f;

            // Thrown out and pulled down, the way struck sparks go.
            ParticleSystem.MainModule main = system.main;
            main.gravityModifier = 1.4f;

            Save(host, SparkPrefabPath);
        }

        private static void BuildConfetti(Material material)
        {
            var host = new GameObject("BayConfetti");
            ParticleSystem system = Burst(host, material, 40, 1.4f, 3.4f, 0.14f,
                Color.white, 0f);

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;

            ParticleSystem.MainModule main = system.main;
            main.gravityModifier = 0.9f;
            main.startColor = Spread();

            Save(host, BayPrefabPath);
        }

        private static void BuildFirework(Material material)
        {
            var host = new GameObject("FinishFirework");
            ParticleSystem system = Burst(host, material, 90, 1.9f, 7f, 0.18f,
                Color.white, 0f);

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            ParticleSystem.MainModule main = system.main;
            main.gravityModifier = 0.55f;
            main.startColor = Spread();

            // A tail on each ember, which is most of what separates a firework from a
            // handful of dots.
            ParticleSystem.TrailModule trails = system.trails;
            trails.enabled = true;
            trails.lifetime = 0.35f;
            trails.widthOverTrail = 0.4f;

            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.trailMaterial = material;

            Save(host, FinishPrefabPath);
        }

        // Every colour of the rainbow, picked per particle.
        private static ParticleSystem.MinMaxGradient Spread()
        {
            var warm = new Color(1f, 0.72f, 0.2f);
            var cool = new Color(0.3f, 0.7f, 1f);

            return new ParticleSystem.MinMaxGradient(warm, cool)
            {
                mode = ParticleSystemGradientMode.TwoColors
            };
        }

        private static ParticleSystem Burst(GameObject host, Material material, int count,
            float lifetime, float speed, float size, Color colour, float emission)
        {
            var system = host.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = system.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = colour;
            main.maxParticles = count * 2;

            // World space, so a burst spawned on a moving car stays where it went off.
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emit = system.emission;
            emit.rateOverTime = 0f;
            emit.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;

            var gradient = new Gradient();

            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });

            fade.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            if (emission > 0f)
            {
                ParticleSystem.LightsModule lights = system.lights;
                lights.enabled = false;
            }

            return system;
        }

        private static void Save(GameObject host, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(host, path);
            Object.DestroyImmediate(host);
        }
    }
}
