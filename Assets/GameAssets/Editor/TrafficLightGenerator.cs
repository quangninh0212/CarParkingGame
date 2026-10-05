using System.IO;
using CarParkingGame.Traffic;
using UnityEditor;
using UnityEngine;

namespace CarParkingGame.EditorTools
{
    // Builds the traffic light head: a pole, a white housing and two round lamps, red over
    // green. No amber - the game asks a driver one question at a time, and TrafficLight
    // shows red through the amber phase when a head has no amber lamp.
    //
    // Each lamp is two objects: a dull lens that is always there, and a bright glow in
    // front of it that the light switches on. That is what gives an unlit lamp something
    // to be rather than a hole in the housing, and it fits TrafficLight's own API, which
    // switches GameObjects rather than materials.
    public static class TrafficLightGenerator
    {
        private const string PrefabFolder = "Assets/GameAssets/Prefabs/Traffic";
        private const string MaterialFolder = "Assets/GameAssets/Materials";

        public const string PrefabPath = PrefabFolder + "/TrafficLight.prefab";

        private static readonly Color Housing = new Color(0.92f, 0.93f, 0.9f);
        private static readonly Color Pole = new Color(0.72f, 0.75f, 0.72f);

        private static readonly Color RedLit = new Color(1f, 0.16f, 0.12f);
        private static readonly Color RedDull = new Color(0.26f, 0.06f, 0.05f);
        private static readonly Color GreenLit = new Color(0.25f, 1f, 0.3f);
        private static readonly Color GreenDull = new Color(0.07f, 0.22f, 0.09f);

        private const float PoleHeight = 2.5f;
        private const float BoxWidth = 0.46f;
        private const float BoxHeight = 0.9f;
        private const float BoxDepth = 0.26f;

        [MenuItem("Tools/Car Parking/Generate Traffic Light")]
        public static void Generate()
        {
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(MaterialFolder);

            var root = new GameObject("TrafficLight");

            Block(root.transform, "Pole", new Vector3(0f, PoleHeight * 0.5f, 0f),
                new Vector3(0.09f, PoleHeight, 0.09f), Pole, false);

            float boxY = PoleHeight + BoxHeight * 0.5f - 0.08f;

            Block(root.transform, "Housing", new Vector3(0f, boxY, 0f),
                new Vector3(BoxWidth, BoxHeight, BoxDepth), Housing, false);

            // On the face of the housing, facing -Z, which is the way a head is hung for
            // traffic coming towards it.
            float lens = BoxWidth * 0.62f;
            float faceZ = -BoxDepth * 0.5f - 0.015f;

            GameObject red = Lamp(root.transform, "Red",
                new Vector3(0f, boxY + BoxHeight * 0.22f, faceZ), lens, RedDull, RedLit);

            GameObject green = Lamp(root.transform, "Green",
                new Vector3(0f, boxY - BoxHeight * 0.22f, faceZ), lens, GreenDull, GreenLit);

            var light = root.AddComponent<TrafficLight>();

            var serialized = new SerializedObject(light);
            serialized.FindProperty("redGlow").objectReferenceValue = red;
            serialized.FindProperty("greenGlow").objectReferenceValue = green;

            // Left empty on purpose. It is what tells TrafficLight to hold red through the
            // amber phase instead of going dark.
            serialized.FindProperty("yellowGlow").objectReferenceValue = null;
            serialized.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            Debug.Log($"[TrafficLightGenerator] Wrote '{PrefabPath}'.");
        }

        public static void GenerateFromCommandLine()
        {
            Generate();
            EditorApplication.Exit(0);
        }

        // The dull lens stays; the glow in front of it is what the light switches. Returned
        // so the caller can hand the glow to TrafficLight.
        private static GameObject Lamp(Transform parent, string name, Vector3 at, float size,
            Color dull, Color lit)
        {
            Block(parent, name + "Lens", at, new Vector3(size, size, 0.05f), dull, true);

            GameObject glow = Block(parent, name + "Glow",
                at + new Vector3(0f, 0f, -0.02f), new Vector3(size * 0.92f, size * 0.92f, 0.04f), lit, true);

            glow.SetActive(false);
            return glow;
        }

        private static GameObject Block(Transform parent, string name, Vector3 at, Vector3 size,
            Color colour, bool round)
        {
            GameObject block = GameObject.CreatePrimitive(round ? PrimitiveType.Sphere : PrimitiveType.Cube);

            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = at;
            block.transform.localScale = size;

            // Primitives ship with a collider. A traffic light the player can hit is a
            // traffic light that scores them a collision penalty from across the car park.
            Collider collider = block.GetComponent<Collider>();

            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            var renderer = block.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = LampMaterial(name, colour, round);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            return block;
        }

        private static Material LampMaterial(string name, Color colour, bool emissive)
        {
            string path = $"{MaterialFolder}/Traffic{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "Traffic" + name };

            material.SetColor("_BaseColor", colour);

            // A lit lamp has to read as lit in daylight, which flat colour alone does not
            // do on a surface this small.
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", colour * 1.6f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
