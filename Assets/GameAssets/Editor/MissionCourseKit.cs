using System.Collections.Generic;
using CarParkingGame.Parking;
using UnityEditor;
using UnityEngine;

namespace CarParkingGame.EditorTools
{
    // The set of parts the authored courses are built from, and the one place that knows
    // which prefab each part is.
    //
    // Everything is specified in metres and assembled in the course's own local space,
    // with +Z the direction the car faces at the start. Sizes are real sizes: the kit
    // measures each prefab's own bounds and scales it to fit, so a wall three metres high
    // is three metres high whatever units the source asset happens to use.
    //
    // Props are spawned as prefab instances rather than copies, which keeps the scene file
    // to a fraction of the size for the same number of objects.
    public class MissionCourseKit
    {
        private const string PrototypePrefabs = "Assets/GameAssets/Prototype Collection/URP/Prefabs/";
        private const string TrackPrefabs = "Assets/GameAssets/CartoonTracksPack1/Track1/Prefabs/Props/";

        public enum Part
        {
            Concrete,
            ConcreteYellow,
            ConcreteRed,
            Kerb,
            Plate,
            PlateYellow,
            Barrier,
            BarrierTall,
            Cone,
            Barrel,
            WaterBarricade,
            Channelizing,
            VerticalPanel,
            PedestrianBarrier,
            Lamp,
            ArrowLong,
            ArrowShort
        }

        public enum CarModel
        {
            Hatchback,
            Sedan,
            Classic,
            Muscle,
            HotRod
        }

        private static readonly Dictionary<Part, string> PartPaths = new Dictionary<Part, string>
        {
            [Part.Concrete] = PrototypePrefabs + "Blocks/Block_Cube_1_Bare.prefab",
            [Part.ConcreteYellow] = PrototypePrefabs + "Blocks/Block_Cube_1_YellowStripes.prefab",
            [Part.ConcreteRed] = PrototypePrefabs + "Blocks/Block_Cube_1_RedStripes.prefab",
            [Part.Kerb] = PrototypePrefabs + "Blocks/Block_Trim_1_Bare.prefab",
            [Part.Plate] = PrototypePrefabs + "Blocks/Block_Parking_1_Bare.prefab",
            [Part.PlateYellow] = PrototypePrefabs + "Blocks/Block_Parking_1_YellowStripes.prefab",
            [Part.Barrier] = PrototypePrefabs + "Blocks/Block_Barrier_1_YellowStripes.prefab",
            [Part.BarrierTall] = PrototypePrefabs + "Blocks/Block_Barrier_2_RedStripes.prefab",
            [Part.Cone] = PrototypePrefabs + "Cone/Cone_Clean.prefab",
            [Part.Barrel] = PrototypePrefabs + "Barrel/Barrel_Used.prefab",
            [Part.WaterBarricade] = PrototypePrefabs + "Water Barricade/Water_Barricade_1_Clean.prefab",
            [Part.Channelizing] = PrototypePrefabs + "Channelizing/Channelizing_Clean.prefab",
            [Part.VerticalPanel] = PrototypePrefabs + "Vertical Panel/Vertical_Panel_Clean.prefab",
            [Part.PedestrianBarrier] = PrototypePrefabs + "Pedestrian Barrier/Pedestrian_Barrier_1_Clean.prefab",
            [Part.Lamp] = TrackPrefabs + "prop_lamp.prefab",
            [Part.ArrowLong] = TrackPrefabs + "prop_arrow_long.prefab",
            [Part.ArrowShort] = TrackPrefabs + "prop_arrow_short.prefab"
        };

        private static readonly Dictionary<CarModel, string> CarPaths = new Dictionary<CarModel, string>
        {
            [CarModel.Hatchback] = "Assets/GameAssets/Hatchback and Sedan/prefabs/HATCHBACK_1988.prefab",
            [CarModel.Sedan] = "Assets/GameAssets/Hatchback and Sedan/prefabs/SEDAN.prefab",
            [CarModel.Classic] = "Assets/GameAssets/50s, 60s and 70s Car Pack (6 Cars)/1950s Classic Car/Prefabs/ClassicCarFull.prefab",
            [CarModel.Muscle] = "Assets/GameAssets/50s, 60s and 70s Car Pack (6 Cars)/1960s Muscle Car/Prefabs/MuscleCar.prefab",
            [CarModel.HotRod] = "Assets/GameAssets/50s, 60s and 70s Car Pack (6 Cars)/1950s Hot Rod/Prefabs/1950sHotRod.prefab"
        };

        // Parts that are paint on the ground. They are spawned without a collider, because
        // a road marking the car bumps into is worse than no road marking.
        private static readonly HashSet<Part> FlatMarkings = new HashSet<Part>
        {
            Part.Plate, Part.PlateYellow, Part.ArrowLong, Part.ArrowShort
        };

        private readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, Vector3> measured = new Dictionary<string, Vector3>();
        private readonly List<string> missing = new List<string>();

        public IReadOnlyList<string> MissingPrefabs => missing;

        // The floor height everything is placed relative to. Courses with more than one
        // deck - the ramps, the multi-storey, the rooftop - move this between sections
        // instead of carrying a y through every call.
        public float BaseY { get; set; }

        public GameObject Spawn(Part part, Transform parent, Vector3 localPosition, float yawDegrees, Vector3 size)
        {
            GameObject instance = SpawnPath(PartPaths[part], parent, localPosition, yawDegrees, size);

            if (instance != null && FlatMarkings.Contains(part))
            {
                StripColliders(instance);
            }
            else if (instance != null)
            {
                EnsureCollider(instance);
            }

            return instance;
        }

        // Natural size: the part keeps its authored proportions and only its height is set,
        // which is what cones, barrels and roadwork props want.
        public GameObject SpawnUpright(Part part, Transform parent, Vector3 localPosition, float yawDegrees, float height)
        {
            Vector3 natural = Measure(PartPaths[part]);

            if (natural.y <= 0.0001f)
            {
                return Spawn(part, parent, localPosition, yawDegrees, Vector3.one);
            }

            float factor = height / natural.y;
            return Spawn(part, parent, localPosition, yawDegrees, natural * factor);
        }

        // A parked car: the art only, with one box collider. The source prefabs are models,
        // but stripping anything drivable off them is cheap insurance against a scene full
        // of cars that all think they are the player's.
        public GameObject SpawnCar(CarModel model, Transform parent, Vector3 localPosition, float yawDegrees)
        {
            GameObject instance = SpawnPath(CarPaths[model], parent, localPosition, yawDegrees, Vector3.zero);

            if (instance == null)
            {
                return null;
            }

            foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Object.DestroyImmediate(behaviour);
            }

            foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true))
            {
                Object.DestroyImmediate(body);
            }

            StripColliders(instance);
            EnsureCollider(instance);

            return instance;
        }

        // ----- composite shapes ---------------------------------------------------------------

        // A wall between two points on the course floor.
        public void Wall(Transform parent, Vector2 from, Vector2 to, float height, float thickness = 0.4f, Part part = Part.Concrete)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;

            if (length < 0.01f)
            {
                return;
            }

            Vector2 centre = (from + to) * 0.5f;
            float yaw = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;

            Spawn(part, parent, new Vector3(centre.x, BaseY + height * 0.5f, centre.y), yaw, new Vector3(thickness, height, length));
        }

        public void Kerb(Transform parent, Vector2 from, Vector2 to, float width = 0.5f)
        {
            Wall(parent, from, to, 0.18f, width, Part.Kerb);
        }

        public void ConeLine(Transform parent, Vector2 from, Vector2 to, int count, float height = 0.75f)
        {
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                Vector2 at = Vector2.Lerp(from, to, t);
                SpawnUpright(Part.Cone, parent, new Vector3(at.x, BaseY, at.y), 0f, height);
            }
        }

        // Cones laid in a zigzag down a lane: the slalom every driving test has.
        public void ConeSlalom(Transform parent, float fromZ, float toZ, float amplitude, int gates)
        {
            for (int i = 0; i < gates; i++)
            {
                float z = Mathf.Lerp(fromZ, toZ, gates == 1 ? 0.5f : i / (float)(gates - 1));
                float x = i % 2 == 0 ? -amplitude : amplitude;

                SpawnUpright(Part.Cone, parent, new Vector3(x, BaseY, z), 0f, 0.75f);
                SpawnUpright(Part.Cone, parent, new Vector3(x, BaseY, z + 1.4f), 0f, 0.75f);
            }
        }

        public void BarrierLine(Transform parent, Vector2 from, Vector2 to, int count, Part part = Part.Barrier)
        {
            Vector3 natural = Measure(PartPaths[part]);

            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                Vector2 at = Vector2.Lerp(from, to, t);

                Vector2 delta = to - from;
                float yaw = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;

                Spawn(part, parent, new Vector3(at.x, BaseY, at.y), yaw, natural);
            }
        }

        public void Pillars(Transform parent, Vector2 from, Vector2 to, int count, float height = 3.2f, float thickness = 0.7f)
        {
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                Vector2 at = Vector2.Lerp(from, to, t);

                Spawn(Part.Concrete, parent, new Vector3(at.x, BaseY + height * 0.5f, at.y), 0f,
                    new Vector3(thickness, height, thickness));
            }
        }

        // A sloped slab the car can drive up or down. Kept gentle: WheelColliders climb a
        // shallow ramp cleanly and catch on a steep one.
        public void Ramp(Transform parent, Vector2 from, Vector2 to, float width, float rise)
        {
            Vector2 delta = to - from;
            float run = delta.magnitude;

            if (run < 0.5f)
            {
                return;
            }

            float length = Mathf.Sqrt(run * run + rise * rise);
            float yaw = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(rise, run) * Mathf.Rad2Deg;

            Vector2 centre = (from + to) * 0.5f;

            GameObject ramp = Spawn(Part.Concrete, parent, new Vector3(centre.x, BaseY + rise * 0.5f, centre.y), yaw,
                new Vector3(width, 0.4f, length));

            if (ramp != null)
            {
                ramp.transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
            }
        }

        // ----- plumbing -------------------------------------------------------------------------

        private GameObject SpawnPath(string path, Transform parent, Vector3 localPosition, float yawDegrees, Vector3 size)
        {
            GameObject prefab = Load(path);

            if (prefab == null)
            {
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);

            if (size != Vector3.zero)
            {
                Vector3 natural = Measure(path);

                instance.transform.localScale = new Vector3(
                    natural.x > 0.0001f ? size.x / natural.x : 1f,
                    natural.y > 0.0001f ? size.y / natural.y : 1f,
                    natural.z > 0.0001f ? size.z / natural.z : 1f);
            }

            return instance;
        }

        private GameObject Load(string path)
        {
            if (prefabs.TryGetValue(path, out GameObject cached))
            {
                return cached;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
            {
                missing.Add(path);
                Debug.LogWarning($"[MissionCourseKit] Missing prefab '{path}'.");
            }

            prefabs[path] = prefab;
            return prefab;
        }

        // The prefab's own size at scale one, so callers can ask for real metres.
        private Vector3 Measure(string path)
        {
            if (measured.TryGetValue(path, out Vector3 cached))
            {
                return cached;
            }

            GameObject prefab = Load(path);
            Vector3 size = Vector3.one;

            if (prefab != null)
            {
                var probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                probe.transform.position = Vector3.zero;
                probe.transform.rotation = Quaternion.identity;
                probe.transform.localScale = Vector3.one;

                bool any = false;
                var bounds = new Bounds();

                foreach (Renderer renderer in probe.GetComponentsInChildren<Renderer>(true))
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

                if (any)
                {
                    size = bounds.size;
                }

                Object.DestroyImmediate(probe);
            }

            measured[path] = size;
            return size;
        }

        // Recolours a spawned part by giving it its own material asset.
        //
        // A MaterialPropertyBlock would have been tidier but it is runtime-only state and
        // is not saved with the scene, so the first attempt at this produced courses that
        // were correctly built and completely invisible: concrete walls on a concrete
        // floor. The material is created once and shared by every part that asks for that
        // colour.
        public static void Tint(GameObject target, string materialName, Color color)
        {
            if (target == null)
            {
                return;
            }

            Material tinted = null;

            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                tinted ??= GetTintedMaterial(materialName, color, renderer.sharedMaterial);

                if (tinted != null)
                {
                    renderer.sharedMaterial = tinted;
                }
            }
        }

        private const string MaterialFolder = "Assets/GameAssets/Materials";

        private static Material GetTintedMaterial(string name, Color color, Material source)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (existing != null)
            {
                return existing;
            }

            Shader shader = source != null
                ? source.shader
                : Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader);

            if (source != null)
            {
                material.CopyPropertiesFromMaterial(source);
            }

            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/GameAssets", "Materials");
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void StripColliders(GameObject target)
        {
            foreach (Collider collider in target.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }
        }

        // Everything solid gets exactly one box, sized to what it looks like. Mesh colliders
        // on a few hundred props is a bill an Android build should not be asked to pay.
        private static void EnsureCollider(GameObject target)
        {
            if (target.GetComponentInChildren<Collider>(true) != null)
            {
                return;
            }

            if (!TryMeasureLocalBounds(target.transform, out Bounds local))
            {
                return;
            }

            var collider = target.AddComponent<BoxCollider>();
            collider.center = local.center;
            collider.size = local.size;
        }

        private static bool TryMeasureLocalBounds(Transform root, out Bounds localBounds)
        {
            localBounds = new Bounds();
            bool any = false;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>();

                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                Bounds mesh = filter.sharedMesh.bounds;
                Matrix4x4 toRoot = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;

                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3(
                        (corner & 1) == 0 ? mesh.min.x : mesh.max.x,
                        (corner & 2) == 0 ? mesh.min.y : mesh.max.y,
                        (corner & 4) == 0 ? mesh.min.z : mesh.max.z);

                    Vector3 inRoot = toRoot.MultiplyPoint3x4(point);

                    if (!any)
                    {
                        localBounds = new Bounds(inRoot, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(inRoot);
                    }
                }
            }

            return any;
        }
    }
}
