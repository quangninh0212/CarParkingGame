using System;
using System.Collections.Generic;
using CarParkingGame.Missions;
using CarParkingGame.Parking;
using CarParkingGame.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarParkingGame.EditorTools
{
    // Builds missions 9-30 as authored courses: a slalom, an L-bend, a supermarket car
    // park, a container yard and so on, one per mission, to the brief in Docs.
    //
    // The previous generator picked a clear rectangle of tarmac and dropped a bay on it,
    // which produced thirty places to park and no levels. These are designed layouts. Each
    // course is a method in MissionCourseLibrary that writes its pieces in its own local
    // space, with +Z the direction the car faces at the start, and each lays its own
    // concrete pad - so a course does not need to find tarmac, only flat empty ground, and
    // the whole infield and the grass outside the circuit become usable.
    //
    // Run MissionCourseShotTool afterwards and look at the sheet. A course that reads
    // wrong from above reads wrong from the driving seat.
    public static class MissionCourseBuilder
    {
        private const string ScenePath = "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity";
        private const string CatalogPath = "Assets/GameAssets/ScriptableObjects/MissionCatalog.asset";

        public const int FirstCourse = 9;
        public const int LastCourse = 30;

        private const float SampleStep = 8f;
        // How much the ground under a course may rise and fall. It is generous because the
        // course does not sit on the ground: it stands on its own plinth, which is cast
        // down to the lowest corner. Demanding genuinely flat ground on a banked circuit
        // rejected 97% of the map and left nowhere to put twenty-two courses.
        private const float FlatnessTolerance = 3f;
        private const float EdgeMargin = 2.5f;

        [MenuItem("Tools/Car Parking/Build Mission Courses 9-30")]
        public static void RunInOpenScene()
        {
            Build();
        }

        public static void RunFromCommandLine()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError($"[MissionCourseBuilder] Could not open '{ScenePath}'.");
                EditorApplication.Exit(1);
                return;
            }

            bool ok = Build();

            if (ok)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[MissionCourseBuilder] Saved '{ScenePath}'.");
            }

            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Build()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MissionCatalog>(CatalogPath);

            if (catalog == null)
            {
                Debug.LogError($"[MissionCourseBuilder] No catalog at '{CatalogPath}'.");
                return false;
            }

            RemoveOldCourses();
            Physics.SyncTransforms();

            var kit = new MissionCourseKit();

            // The courses are laid out together as one site rather than scattered over the
            // circuit.
            //
            // Scattering was tried and does not work here: twenty-two courses need
            // twenty-two flat, empty, tree-free plots of forty metres and up, and this map
            // is a wooded racetrack. Searching every plot on the map at four headings
            // placed nine of them. Packed into one site the whole set fits with room to
            // spare, it costs nothing to find, it puts the levels back in the same place
            // on every rebuild, and a driving school laid out as a driving school reads
            // better than twenty-two car parks dropped in the undergrowth.
            var campusSize = new Vector2(CampusRowWidth, 0f);
            PackCampus(Vector3.zero, campusSize, out Bounds needed);

            if (!TryFindCampus(needed.size, out Vector3 campusOrigin))
            {
                Debug.LogError("[MissionCourseBuilder] Could not find anywhere to put the training site.");
                return false;
            }

            List<Plot> plots = PackCampus(campusOrigin, campusSize, out Bounds used);

            LayCampusApron(kit, used);

            int built = 0;

            foreach (Plot plot in plots)
            {
                MissionDefinition definition = catalog.Find(plot.course.missionId);

                if (definition == null)
                {
                    Debug.LogWarning($"[MissionCourseBuilder] No definition for mission {plot.course.missionId}; skipped.");
                    continue;
                }

                ApplyDefinition(plot.course, definition);
                BuildCourse(plot.course, definition, kit, plot.position, plot.yaw, 0f);
                built++;
            }

            Debug.Log($"[MissionCourseBuilder] Built {built} of {MissionCourseLibrary.All.Count} courses on a {used.size.x:0}x{used.size.z:0}m site at {used.center:0}.");
            return built > 0;
        }

        private readonly struct Plot
        {
            public readonly MissionCourseLibrary.Course course;
            public readonly Vector3 position;
            public readonly float yaw;

            public Plot(MissionCourseLibrary.Course course, Vector3 position, float yaw)
            {
                this.course = course;
                this.position = position;
                this.yaw = yaw;
            }
        }

        private const float CourseGap = 8f;
        private const float ApronMargin = 8f;
        private const float CampusRowWidth = 230f;

        // Puts the training site on the open ground off the south edge of the circuit,
        // lapped far enough onto the terrain that the player can drive across to it.
        //
        // It is anchored to the edge rather than searched for. The site needs a clear,
        // level rectangle a quarter of a kilometre on a side and this map has nothing of
        // the sort: a sweep of every plot on it rejected 1493 of 1496 outright. Off the
        // edge there is nothing in the way by definition, so the only question left is how
        // far out it has to sit, and that is answered by stepping outward until clear.
        private static bool TryFindCampus(Vector3 needed, out Vector3 origin)
        {
            origin = default;

            Bounds ground = MeasureGroundArea(out bool measured);

            if (!measured)
            {
                return false;
            }

            Vector3 anchor = MeasureExistingMissionCentre(ground.center);

            var footprint = new Vector3(needed.x + ApronMargin * 2f, 6f, needed.z + ApronMargin * 2f);
            var half = new Vector3(footprint.x * 0.5f, 3f, footprint.z * 0.5f);

            // Lined up under the existing missions, so a challenge run does not cross the
            // whole map to reach it.
            float centreX = Mathf.Clamp(anchor.x, ground.min.x + half.x, ground.max.x - half.x);

            // Enough overlap to join the terrain, then pushed out until nothing is in it.
            const float StartOverlap = 40f;

            if (!TryEdgeHeight(centreX, ground, out float height))
            {
                return false;
            }

            for (int step = 0; step < 24; step++)
            {
                float centreZ = ground.min.z + StartOverlap - half.z - step * 20f;
                var centre = new Vector3(centreX, height + 3.3f, centreZ);

                if (!IsFootprintClear(centre, half))
                {
                    continue;
                }

                origin = new Vector3(centreX - needed.x * 0.5f, height, centreZ - needed.z * 0.5f);
                return true;
            }

            Debug.LogWarning($"[MissionCourseBuilder] Nothing clear enough for a {footprint.x:0}x{footprint.z:0}m site off the south edge.");
            return false;
        }

        // The ground height just inside the circuit's south edge, which is the height the
        // site is built at so the two meet.
        private static bool TryEdgeHeight(float x, Bounds ground, out float height)
        {
            height = 0f;

            // The bounding box's south edge is not necessarily where the ground starts, so
            // this walks north until it finds a row that is actually on the circuit.
            for (float z = ground.min.z; z < ground.center.z; z += 20f)
            {
                var heights = new List<float>();

                for (int i = 1; i <= 9; i++)
                {
                    var at = new Vector3(x + (i - 5) * 12f, 250f, z);

                    if (Physics.Raycast(at, Vector3.down, out RaycastHit hit, 500f) && IsGround(hit.collider.name))
                    {
                        heights.Add(hit.point.y);
                    }
                }

                if (heights.Count < 5)
                {
                    continue;
                }

                heights.Sort();
                height = heights[heights.Count / 2] + 0.02f;
                return true;
            }

            return false;
        }

        private static bool IsFootprintClear(Vector3 centre, Vector3 halfExtents)
        {
            foreach (Collider hit in Physics.OverlapBox(centre, halfExtents, Quaternion.identity))
            {
                if (hit.isTrigger || IsGround(hit.name))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static Vector3 MeasureExistingMissionCentre(Vector3 fallback)
        {
            var centre = Vector3.zero;
            int count = 0;

            foreach (MissionAuthoring authoring in UnityEngine.Object.FindObjectsByType<MissionAuthoring>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (authoring.MissionId < FirstCourse && authoring.ParkingZone != null)
                {
                    centre += authoring.ParkingZone.WorldCenter;
                    count++;
                }
            }

            return count > 0 ? centre / count : fallback;
        }

        // The height to build the site at: the highest ground anywhere under it, so the
        // slab sits on top of the terrain rather than through it. Rejected outright if the
        // ground is missing anywhere under the footprint, or falls away by more than a
        // storey across it.
        private static bool TrySiteHeight(Vector2 centre, Vector3 footprint, out float height)
        {
            height = 0f;

            float lowest = float.MaxValue;
            float highest = float.MinValue;
            int onGround = 0;
            int samples = 0;

            for (int ix = -2; ix <= 2; ix++)
            {
                for (int iz = -2; iz <= 2; iz++)
                {
                    var at = new Vector3(
                        centre.x + ix * footprint.x * 0.25f,
                        0f,
                        centre.y + iz * footprint.z * 0.25f);

                    samples++;

                    if (!Physics.Raycast(new Vector3(at.x, 250f, at.z), Vector3.down, out RaycastHit hit, 500f)
                        || !IsGround(hit.collider.name))
                    {
                        continue;
                    }

                    onGround++;
                    lowest = Mathf.Min(lowest, hit.point.y);
                    highest = Mathf.Max(highest, hit.point.y);
                }
            }

            // Most of the plot has to be over the circuit's own ground, and it has to be
            // within a couple of storeys end to end. The slab makes up the rest.
            if (onGround < samples * 0.7f || highest - lowest > 7f)
            {
                return false;
            }

            height = highest + 0.02f;
            return true;
        }

        // Packs the courses into rows, tallest row first, like plots on a trading estate.
        // Every course faces the same way, so the whole site reads as one place.
        private static List<Plot> PackCampus(Vector3 origin, Vector2 size, out Bounds used)
        {
            var order = new List<MissionCourseLibrary.Course>(MissionCourseLibrary.All);
            order.Sort((a, b) => b.length.CompareTo(a.length));

            var plots = new List<Plot>();

            float x = 0f;
            float z = 0f;
            float rowDepth = 0f;
            float widest = 0f;

            foreach (MissionCourseLibrary.Course course in order)
            {
                if (x > 0f && x + course.width > size.x)
                {
                    x = 0f;
                    z += rowDepth + CourseGap;
                    rowDepth = 0f;
                }

                // The course's own origin is its entrance, at the near edge and centred.
                plots.Add(new Plot(course, origin + new Vector3(x + course.width * 0.5f, 0f, z), 0f));

                x += course.width + CourseGap;
                widest = Mathf.Max(widest, x - CourseGap);
                rowDepth = Mathf.Max(rowDepth, course.length);
            }

            float depth = z + rowDepth;

            used = new Bounds(
                origin + new Vector3(widest * 0.5f, 0f, depth * 0.5f),
                new Vector3(widest, 1f, depth));

            return plots;
        }

        // One slab under the whole site, so the courses stand on a surface rather than
        // floating over whatever happens to be below them.
        private static void LayCampusApron(MissionCourseKit kit, Bounds used)
        {
            var apron = new GameObject("TrainingSiteApron");
            Undo.RegisterCreatedObjectUndo(apron, "Lay training site");
            apron.transform.position = used.center;

            kit.BaseY = 0f;

            // Thick enough to reach the ground at the low end of the plot. The site is
            // built at the highest point under it, so a thin slab would hang in the air
            // wherever the terrain falls away.
            const float thickness = 7f;

            GameObject slab = kit.Spawn(MissionCourseKit.Part.Concrete, apron.transform,
                new Vector3(0f, -thickness * 0.5f, 0f), 0f,
                new Vector3(used.size.x + ApronMargin * 2f, thickness, used.size.z + ApronMargin * 2f));

            // Lighter than the courses that stand on it, so each course reads as its own
            // plot rather than dissolving into the site.
            MissionCourseKit.Tint(slab, "CourseApron", new Color(0.36f, 0.36f, 0.38f));

            // A wall round the whole site. The slab stands clear of the terrain, so
            // without this, running wide anywhere on it drops the car off the world.
            float halfWidth = used.size.x * 0.5f + ApronMargin;
            float halfLength = used.size.z * 0.5f + ApronMargin;

            var corners = new[]
            {
                new Vector2(-halfWidth, -halfLength),
                new Vector2(halfWidth, -halfLength),
                new Vector2(halfWidth, halfLength),
                new Vector2(-halfWidth, halfLength)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                kit.Wall(apron.transform, corners[i], corners[(i + 1) % corners.Length], 2.5f, 0.8f);
            }
        }

        // The course owns its own mission data. The catalog's old entries were written for
        // the generated bays and describe levels that no longer exist, so name, type and
        // difficulty come from the design rather than the other way round.
        //
        // Practice runs these untimed: challenge mode puts its own clock on every stage,
        // and a time limit on a level nobody has driven yet is a guess at best.
        private static void ApplyDefinition(MissionCourseLibrary.Course course, MissionDefinition definition)
        {
            var serialized = new SerializedObject(definition);
            SerializedProperty rules = serialized.FindProperty("scoreRules");

            definition.EditorConfigure(
                course.missionId,
                course.name,
                course.description,
                course.difficulty,
                course.parkingType,
                // Untimed in practice; challenge mode brings its own clock.
                0f,
                true,
                2f,
                20f,
                1.5f,
                0.9f,

                // The heading is enforced rather than allowed either way round, because the
                // bay now has an arrow painted in it saying which way to face. Twenty degrees
                // of tolerance keeps it fair.
                false,
                200 + course.difficulty * 100,
                rules != null ? rules.objectReferenceValue as ScoreRules : null);

            EditorUtility.SetDirty(definition);
        }

        // ----- placement -------------------------------------------------------------------

        private static void RemoveOldCourses()
        {
            foreach (MissionAuthoring authoring in UnityEngine.Object.FindObjectsByType<MissionAuthoring>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (authoring.MissionId >= FirstCourse)
                {
                    UnityEngine.Object.DestroyImmediate(authoring.gameObject);
                }
            }

            GameObject apron = GameObject.Find("TrainingSiteApron");

            if (apron != null)
            {
                UnityEngine.Object.DestroyImmediate(apron);
            }
        }

        // The surfaces that are actually the ground of the circuit.
        //
        // Plane001 and underground are deliberately absent: they are a backdrop plane and
        // a skirt under the whole map, hundreds of metres across, and measuring the
        // terrain through them put the training site 450m off the side of the world at a
        // height taken from the grandstand roofs.
        private static readonly HashSet<string> GroundNames = new HashSet<string>
        {
            "0GRASS", "0GRASS2", "1GRASS", "1GRASS_2", "1GRAVEL", "1CONCRETE_red",
            "1PITLANE", "1TARMAC_back", "1TARMAC_inner", "1TARMAC_oval", "1TARMACgarages",
            "sand_outside", "kerbs"
        };

        private static bool IsGround(string name)
        {
            return GroundNames.Contains(name);
        }

        // Measures the surface height under a point.
        private static bool TryGroundHeight(Vector3 at, out float groundY)
        {
            groundY = 0f;

            var origin = new Vector3(at.x, 250f, at.z);

            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 500f))
            {
                return false;
            }

            groundY = hit.point.y;
            return true;
        }

        private static Bounds MeasureGroundArea(out bool measured)
        {
            var bounds = new Bounds();
            measured = false;

            foreach (MeshRenderer renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!IsGround(renderer.name))
                {
                    continue;
                }

                if (!measured)
                {
                    bounds = renderer.bounds;
                    measured = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

        // ----- assembly ---------------------------------------------------------------------

        private static void BuildCourse(
            MissionCourseLibrary.Course course,
            MissionDefinition definition,
            MissionCourseKit kit,
            Vector3 position,
            float yaw,
            float drop)
        {
            var root = new GameObject($"Mission{course.missionId:00}_{course.name.Replace(' ', '_')}");
            Undo.RegisterCreatedObjectUndo(root, "Build mission course");

            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            var writer = new CourseWriter(root.transform, kit, course);
            writer.Level(0f);

            // Courses with more than one deck lay their own floors: a single slab over the
            // whole footprint would be a ceiling over the lower level, and the ramp down
            // from it would run straight into the underside of the course.
            if (course.singleDeck)
            {
                writer.Pad(drop);
            }

            course.layout(writer);

            // After the layout, so the entrance gap can be put where the course's own
            // start point is rather than guessed at.
            if (course.singleDeck)
            {
                float entranceX = writer.StartPoint != null ? writer.StartPoint.localPosition.x : 0f;
                writer.Rail(0f, course.length * 0.5f, course.width, course.length, 12f, entranceX);
            }

            if (writer.Zone == null)
            {
                Debug.LogError($"[MissionCourseBuilder] Mission {course.missionId} ('{course.name}') laid out no parking bay; removed.", root);
                UnityEngine.Object.DestroyImmediate(root);
                return;
            }

            Transform startPoint = writer.StartPoint != null ? writer.StartPoint : writer.DefaultStart();

            var authoring = root.AddComponent<MissionAuthoring>();
            authoring.EditorAssign(definition, startPoint, writer.Zone, root);
            EditorUtility.SetDirty(authoring);

            root.SetActive(false);

            Debug.Log($"[MissionCourseBuilder] Mission {course.missionId} '{course.name}' ({course.width:0}x{course.length:0}m, {definition.ParkingType}) at {position:0.0} yaw {yaw:0}.", root);
        }

        // The thing each course's layout method writes into. Everything is in course-local
        // metres with +Z forward from the entrance, which is what lets the layouts below
        public class CourseWriter
        {
            private static readonly Color Tarmac = new Color(0.21f, 0.21f, 0.23f);
            private static readonly Color Apron = new Color(0.34f, 0.34f, 0.36f);

            private readonly Transform root;
            private readonly MissionCourseKit kit;
            private readonly MissionCourseLibrary.Course course;

            public CourseWriter(Transform root, MissionCourseKit kit, MissionCourseLibrary.Course course)
            {
                this.root = root;
                this.kit = kit;
                this.course = course;
            }

            private float level;

            public ParkingZone Zone { get; private set; }
            public Transform StartPoint { get; private set; }
            public MissionCourseKit Kit => kit;
            public Transform Root => root;

            public float Width => course.width;
            public float Length => course.length;

            // The floor height everything after this call sits on. Courses with more than
            // one deck move it between sections rather than carrying a height through
            // every single call.
            public void Level(float y)
            {
                level = y;
                kit.BaseY = y;
            }

            // The concrete the course stands on, cast at the plot's high corner and thick
            // enough to reach its low one. The top is 2cm proud of the high corner: enough
            // to read as a surface, little enough to drive onto from that side.
            public void Pad(float groundDrop = 0f)
            {
                PadAt(0f, course.length * 0.5f, course.width, course.length, groundDrop + 0.5f);
            }

            // Cast from the solid block, not from the flat marking part: the kit strips
            // colliders from markings, because a painted line the car bumps into is worse
            // than no line. Spawned as a marking the pad had no collider at all and the
            // car dropped straight through the floor of the course.
            // A wall round the edge of a deck, so the player cannot drive off it.
            //
            // Every course stands on a raised slab; before this, running wide at the edge
            // dropped the car off the world. The entrance gap is only left on the deck the
            // course starts on.
            public void Rail(float x, float z, float width, float length, float entranceGap = 0f, float entranceCentreX = 0f)
            {
                const float Height = 1.6f;

                float left = x - width * 0.5f;
                float right = x + width * 0.5f;
                float near = z - length * 0.5f;
                float far = z + length * 0.5f;

                kit.Wall(root, new Vector2(left, near), new Vector2(left, far), Height, 0.5f);
                kit.Wall(root, new Vector2(right, near), new Vector2(right, far), Height, 0.5f);
                kit.Wall(root, new Vector2(left, far), new Vector2(right, far), Height, 0.5f);

                if (entranceGap <= 0f)
                {
                    kit.Wall(root, new Vector2(left, near), new Vector2(right, near), Height, 0.5f);
                    return;
                }

                // The gap goes where the course's entrance actually is. Centring it blindly
                // walled the start point in on every course that begins off to one side.
                float gapLeft = Mathf.Clamp(entranceCentreX - entranceGap * 0.5f, left, right);
                float gapRight = Mathf.Clamp(entranceCentreX + entranceGap * 0.5f, left, right);

                kit.Wall(root, new Vector2(left, near), new Vector2(gapLeft, near), Height, 0.5f);
                kit.Wall(root, new Vector2(gapRight, near), new Vector2(right, near), Height, 0.5f);
            }

            public void PadAt(float x, float z, float width, float length, float thickness = 0.5f)
            {
                GameObject pad = kit.Spawn(MissionCourseKit.Part.Concrete, root,
                    new Vector3(x, level + 0.02f - thickness * 0.5f, z), 0f,
                    new Vector3(width, thickness, length));

                // Darkened to read as tarmac. Everything in the kit is cast from the same
                // pale grey block, so without this a course is concrete walls on a
                // concrete floor and none of the layout is legible.
                MissionCourseKit.Tint(pad, "CourseTarmac", Tarmac);
            }

            public void Wall(float x1, float z1, float x2, float z2, float height = 2.4f,
                float thickness = 0.4f, MissionCourseKit.Part part = MissionCourseKit.Part.Concrete)
            {
                kit.Wall(root, new Vector2(x1, z1), new Vector2(x2, z2), height, thickness, part);
            }

            public void Kerb(float x1, float z1, float x2, float z2)
            {
                kit.Kerb(root, new Vector2(x1, z1), new Vector2(x2, z2));
            }

            public void Cones(float x1, float z1, float x2, float z2, int count)
            {
                kit.ConeLine(root, new Vector2(x1, z1), new Vector2(x2, z2), count);
            }

            public void Slalom(float fromZ, float toZ, float amplitude, int gates)
            {
                kit.ConeSlalom(root, fromZ, toZ, amplitude, gates);
            }

            public void Barriers(float x1, float z1, float x2, float z2, int count)
            {
                kit.BarrierLine(root, new Vector2(x1, z1), new Vector2(x2, z2), count);
            }

            public void Pillars(float x1, float z1, float x2, float z2, int count, float height = 3.2f)
            {
                kit.Pillars(root, new Vector2(x1, z1), new Vector2(x2, z2), count, height);
            }

            public void Ramp(float x1, float z1, float x2, float z2, float width, float rise)
            {
                kit.Ramp(root, new Vector2(x1, z1), new Vector2(x2, z2), width, rise);
            }

            public void Car(float x, float z, float yaw, MissionCourseKit.CarModel model = MissionCourseKit.CarModel.Sedan)
            {
                kit.SpawnCar(model, root, new Vector3(x, level, z), yaw);
            }

            // A row of parked cars, which is what makes a car park look like a car park.
            public void CarRow(float x, float z, float yaw, int count, float spacing, bool alongZ = false)
            {
                var models = new[]
                {
                    MissionCourseKit.CarModel.Sedan,
                    MissionCourseKit.CarModel.Hatchback,
                    MissionCourseKit.CarModel.Classic,
                    MissionCourseKit.CarModel.Muscle,
                    MissionCourseKit.CarModel.HotRod
                };

                for (int i = 0; i < count; i++)
                {
                    float offset = i * spacing;
                    Car(alongZ ? x : x + offset, alongZ ? z + offset : z, yaw, models[i % models.Length]);
                }
            }

            public void Prop(MissionCourseKit.Part part, float x, float z, float yaw, float height)
            {
                kit.SpawnUpright(part, root, new Vector3(x, level, z), yaw, height);
            }

            // A solid block of a given size, which stands in for everything the project has
            // no model of: shipping containers, fuel pumps, buses, warehouse walls.
            public void Block(float x, float z, float yaw, float width, float height, float depth,
                MissionCourseKit.Part part = MissionCourseKit.Part.Concrete)
            {
                kit.Spawn(part, root, new Vector3(x, level + height * 0.5f, z), yaw, new Vector3(width, height, depth));
            }

            public void Arrow(float x, float z, float yaw)
            {
                kit.Spawn(MissionCourseKit.Part.ArrowLong, root, new Vector3(x, level + 0.06f, z), yaw, Vector3.zero);
            }

            public void Start(float x, float z, float yaw = 0f)
            {
                var startPoint = new GameObject("StartPoint");
                startPoint.transform.SetParent(root, false);
                startPoint.transform.localPosition = new Vector3(x, level + 1f, z);
                startPoint.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

                StartPoint = startPoint.transform;
            }

            public Transform DefaultStart()
            {
                Start(0f, 3f);
                return StartPoint;
            }

            // The bay itself. Painted so the player can see it, and given the ParkingZone
            // the validator measures against.
            public void Bay(float x, float z, float yaw, float width = 3.2f, float length = 6.5f)
            {
                var bayObject = new GameObject("ParkingBay");
                bayObject.transform.SetParent(root, false);
                bayObject.transform.localPosition = new Vector3(x, level, z);
                bayObject.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

                var zone = bayObject.AddComponent<ParkingZone>();
                zone.EditorSetBox(Vector3.zero, new Vector3(width, 2.5f, length));
                zone.EditorSetParkedFacingBackward(false);

                kit.PaintBay(bayObject.transform, width, length);

                Zone = zone;
            }
        }
    }
}
