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
        private readonly Dictionary<string, Vector3> centres = new Dictionary<string, Vector3>();
        private readonly List<string> missing = new List<string>();

        public IReadOnlyList<string> MissingPrefabs => missing;

        // The floor height everything is placed relative to. Courses with more than one
        // deck - the ramps, the multi-storey, the rooftop - move this between sections
        // instead of carrying a y through every call.
        public float BaseY { get; set; }

        // What a car parked as scenery is called, so a check written later can pick them
        // out of a course without knowing which prefabs the kit happens to use.
        public const string ParkedCarPrefix = "ParkedCar ";

        // Road paint, so: wide and all but flat. A narrow strip standing 10cm proud is a
        // kerb seen edge on from a driver's eye - all shadowed side, almost no top face -
        // and it reads as a dark line rather than as paint.
        private const float PaintThickness = 0.03f;
        private const float PaintLift = 0.035f;

        private static readonly Color Yellow = new Color(1f, 0.82f, 0.15f);
        private static readonly Color White = new Color(0.93f, 0.95f, 0.98f);
        private static readonly Color Hedge = new Color(0.28f, 0.52f, 0.26f);

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
            string path = CarPaths[model];
            GameObject instance = SpawnPath(path, parent, localPosition, yawDegrees, Vector3.zero);

            if (instance == null)
            {
                return null;
            }

            // Stood on the road, not hung off its own pivot.
            //
            // The five car prefabs put their origins in five different places - some at
            // the wheel contact patch, some on the axle line, some at the middle of the
            // body - so placing them all by the pivot stood a few of them correctly and
            // sank the rest into the tarmac up to the sills. Nothing about a prefab's
            // origin is worth trusting; where its tyres are is measurable.
            Vector3 natural = Measure(path);
            Vector3 centre = MeasureCentre(path);
            float bottom = centre.y - natural.y * 0.5f;

            instance.transform.localPosition = localPosition - Vector3.up * bottom;

            // Named so the course checker can find them and confirm they are standing on
            // something.
            instance.name = ParkedCarPrefix + model;

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

            GameObject wall = Spawn(part, parent, new Vector3(centre.x, BaseY + height * 0.5f, centre.y), yaw, new Vector3(thickness, height, length));

            if (part == Part.Concrete)
            {
                Tint(wall, "CourseConcrete", new Color(0.74f, 0.73f, 0.70f));
            }
        }

        // A bay as the player sees it: a closed rectangle with an arrow inside it pointing
        // the way the car's nose has to end up, drawn around the parent's own origin and
        // along its +Z.
        //
        // Three sides and no arrow was not playable - from inside the car there is no way
        // to tell whether a bay wants you nose in or tail in, and guessing wrong fails the
        // heading check with nothing on screen to say why.
        public void PaintBay(Transform bay, float width, float length)
        {
            PaintBay(bay, width, length, true, true);
        }

        // A bay as the player reads it. The colour says whether this is one to aim for,
        // and the arrow - or the lack of one - says whether it has to be entered a
        // particular way round.
        //
        // No arrow means no heading check, and those two have to be decided together: an
        // arrow the validator ignores, or a heading check with nothing on the ground to
        // say which way, are both just a mission the player cannot read.
        public void PaintBay(Transform bay, float width, float length, bool target, bool withArrow)
        {
            const float Paint = 0.32f;

            Color colour = target ? Yellow : White;

            float halfWidth = width * 0.5f;
            float halfLength = length * 0.5f;

            PaintStroke(bay, new Vector3(-halfWidth, PaintLift, 0f), 0f, new Vector3(Paint, PaintThickness, length), colour);
            PaintStroke(bay, new Vector3(halfWidth, PaintLift, 0f), 0f, new Vector3(Paint, PaintThickness, length), colour);
            PaintStroke(bay, new Vector3(0f, PaintLift, halfLength), 0f, new Vector3(width, PaintThickness, Paint), colour);
            PaintStroke(bay, new Vector3(0f, PaintLift, -halfLength), 0f, new Vector3(width, PaintThickness, Paint), colour);

            if (!withArrow)
            {
                return;
            }

            // The arrow sits well inside the rectangle, so its tip does not run into the
            // painted end of the bay.
            PaintArrowHead(bay, length * 0.64f, width * 0.5f, 0.55f, colour);
        }

        // A lane arrow painted on the road, pointing along the parent's +Z: the same flat
        // strokes as a bay's arrow, in white so it reads as a direction marking rather
        // than as part of a bay.
        //
        // This was the track pack's own arrow prop until a player reported the marking at
        // the start of a course "floating above the ground". That prop is a solid board,
        // not a decal, so laying it on the tarmac leaves a plank across the lane with a
        // shadow under it - nothing like paint.
        public void PaintArrow(Transform parent, Vector3 localPosition, float yawDegrees,
                               float length = 4.2f, float width = 1.5f)
        {
            var arrow = new GameObject("LaneArrow");
            Undo.RegisterCreatedObjectUndo(arrow, "Paint lane arrow");

            arrow.transform.SetParent(parent, false);
            arrow.transform.localPosition = localPosition;
            arrow.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);

            PaintArrowHead(arrow.transform, length, width, 0.4f, White);
        }

        // The shaft and the two splayed bars of an arrow, drawn around the parent's origin
        // and pointing along its +Z, so a bay's arrow and a lane's arrow are the same
        // drawing at two sizes.
        private void PaintArrowHead(Transform parent, float length, float width, float stroke, Color colour)
        {
            const float Spread = 38f * Mathf.Deg2Rad;

            // How far back the head reaches, so the shaft can stop where the bars start
            // instead of poking out past the tip.
            float bar = width / (2f * Mathf.Sin(Spread));
            float depth = bar * Mathf.Cos(Spread);

            float tipZ = length * 0.5f;
            float shaft = Mathf.Max(stroke, length - depth);

            PaintStroke(parent, new Vector3(0f, PaintLift, tipZ - depth - shaft * 0.5f), 0f,
                new Vector3(stroke, PaintThickness, shaft), colour);

            for (int side = -1; side <= 1; side += 2)
            {
                var direction = new Vector2(side * Mathf.Sin(Spread), -Mathf.Cos(Spread));

                var centre = new Vector3(direction.x, 0f, direction.y) * (bar * 0.5f)
                    + new Vector3(0f, PaintLift, tipZ);
                float yaw = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;

                PaintStroke(parent, centre, yaw, new Vector3(stroke, PaintThickness, bar), colour);
            }
        }

        private void PaintStroke(Transform bay, Vector3 position, float yaw, Vector3 size)
        {
            PaintStroke(bay, position, yaw, size, Yellow);
        }

        private void PaintStroke(Transform parent, Vector3 position, float yaw, Vector3 size, Color colour)
        {
            GameObject stroke = Spawn(Part.Plate, parent, position, yaw, size);

            // Self-lit, because flat paint on a dark pad under this scene's lighting
            // renders near black whatever colour it is given.
            Tint(stroke, colour == White ? "CourseLanePaint" : "CourseBayPaint", colour, true);
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

                GameObject pillar = Spawn(Part.Concrete, parent, new Vector3(at.x, BaseY + height * 0.5f, at.y), 0f,
                    new Vector3(thickness, height, thickness));

                Tint(pillar, "CourseConcrete", new Color(0.74f, 0.73f, 0.70f));
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

                // Put the visible mesh where the caller asked for it, not the pivot.
                //
                // These prefabs are not centred on their own origins, and the kit scales
                // them by up to ninety times: an offset of a few centimetres in the asset
                // becomes metres on the course. It is why the painted bay lines were
                // nowhere near the bay.
                Vector3 centre = MeasureCentre(path);
                Vector3 scaled = Vector3.Scale(centre, instance.transform.localScale);

                instance.transform.localPosition = localPosition - Quaternion.Euler(0f, yawDegrees, 0f) * scaled;
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
                    centres[path] = bounds.center;
                }

                Object.DestroyImmediate(probe);
            }

            measured[path] = size;
            return size;
        }

        // Where the prefab's visible mesh sits relative to its own origin. Several of
        // these assets are modelled off-pivot by a few centimetres, which the kit's
        // scaling then multiplies into metres.
        private Vector3 MeasureCentre(string path)
        {
            if (!centres.ContainsKey(path))
            {
                Measure(path);
            }

            return centres.TryGetValue(path, out Vector3 centre) ? centre : Vector3.zero;
        }

        // Recolours a spawned part by giving it its own material asset.
        //
        // A MaterialPropertyBlock would have been tidier but it is runtime-only state and
        // is not saved with the scene, so the first attempt at this produced courses that
        // were correctly built and completely invisible: concrete walls on a concrete
        // floor. The material is created once and shared by every part that asks for that
        // colour.
        public static void Tint(GameObject target, string materialName, Color color, bool selfLit = false)
        {
            if (target == null)
            {
                return;
            }

            Material tinted = null;

            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                tinted ??= GetTintedMaterial(materialName, color, renderer.sharedMaterial, selfLit);

                if (tinted != null)
                {
                    // Every slot, not just the first: some of these meshes have more than
                    // one submesh, and recolouring slot zero alone leaves the rest as they
                    // were.
                    var slots = new Material[renderer.sharedMaterials.Length];

                    for (int i = 0; i < slots.Length; i++)
                    {
                        slots[i] = tinted;
                    }

                    renderer.sharedMaterials = slots;
                }
            }
        }

        private const string MaterialFolder = "Assets/GameAssets/Materials";

        private static Material GetTintedMaterial(string name, Color color, Material source, bool selfLit = false)
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

            // Flat colour, every map cleared. The kit scales one small block into pads and
            // walls tens of metres long, which stretches its textures up to ninety times
            // in one axis: with the albedo on, the pads were grey smears and the painted
            // bay lines were stretched until they were not there at all; with only the
            // albedo cleared, the normal map was still there and lit the surface as if it
            // were rippled sheet metal.
            foreach (string property in material.GetTexturePropertyNames())
            {
                material.SetTexture(property, null);
            }

            // Matte, like road surface and concrete. The source blocks are shiny enough to
            // throw a highlight across a twenty-metre pad.
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.05f);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.05f);
            }

            // Road markings are lit by the material itself. The courses sit under the
            // circuit's existing lighting, which leaves a flat ground plane very dark, and
            // a bay the player cannot see is a bay they cannot park in.
            if (selfLit)
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                material.SetColor("_EmissionColor", color * 0.85f);
            }

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
