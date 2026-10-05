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
                MissionDefinition definition = GetOrCreateDefinition(catalog, plot.course.missionId);

                if (definition == null)
                {
                    Debug.LogWarning($"[MissionCourseBuilder] No definition for mission {plot.course.missionId}; skipped.");
                    continue;
                }

                ApplyDefinition(plot.course, definition);
                BuildCourse(plot.course, definition, kit, plot.position, plot.yaw, 0f);
                built++;
            }

            RebuildCatalog(catalog);

            Debug.Log($"[MissionCourseBuilder] Built {built} of {MissionCourseLibrary.All.Count} courses on a {used.size.x:0}x{used.size.z:0}m site at {used.center:0}.");
            return built > 0;
        }

        private const string DefinitionFolder = "Assets/GameAssets/ScriptableObjects/Missions";

        // A level the library has added since the last build has no definition asset yet,
        // so one is made for it. ApplyDefinition fills it in straight afterwards; this only
        // has to exist and be findable.
        private static MissionDefinition GetOrCreateDefinition(MissionCatalog catalog, int missionId)
        {
            MissionDefinition definition = catalog.Find(missionId);

            if (definition != null)
            {
                return definition;
            }

            string path = $"{DefinitionFolder}/Mission{missionId:00}.asset";
            definition = AssetDatabase.LoadAssetAtPath<MissionDefinition>(path);

            if (definition != null)
            {
                return definition;
            }

            definition = ScriptableObject.CreateInstance<MissionDefinition>();
            AssetDatabase.CreateAsset(definition, path);

            Debug.Log($"[MissionCourseBuilder] Mission {missionId} is new; wrote '{path}'.");
            return definition;
        }

        // Every definition on disk, in mission order. The catalog is what the practice
        // screen lists, so a level that is not in it cannot be chosen however well it
        // builds - and the order it is in is the order the levels are played.
        private static void RebuildCatalog(MissionCatalog catalog)
        {
            var found = new List<MissionDefinition>();

            foreach (string guid in AssetDatabase.FindAssets("t:MissionDefinition", new[] { DefinitionFolder }))
            {
                var definition = AssetDatabase.LoadAssetAtPath<MissionDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));

                if (definition != null)
                {
                    found.Add(definition);
                }
            }

            found.Sort((a, b) => a.MissionId.CompareTo(b.MissionId));

            catalog.EditorSetMissions(found);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Debug.Log($"[MissionCourseBuilder] Catalog lists {found.Count} missions.");
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
            // The city and the streets used to be laid across the whole site. They are
            // built into each course now, so the old site-wide objects have to go or the
            // scene keeps both.
            foreach (string stale in new[] { "SiteTraffic", "CitySurround" })
            {
                GameObject found = GameObject.Find(stale);

                if (found != null)
                {
                    UnityEngine.Object.DestroyImmediate(found);
                }
            }

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
            if (course.singleDeck && course.arena)
            {
                // A car park, not a road: walled the whole way round in red and white so it
                // reads as the edge of the lot rather than as a building. Low enough to see
                // the whole lot over from the driving camera, high enough that the car stops
                // against it instead of riding up and out.
                writer.StripedRail(course.width, course.length);
                writer.Surround();
            }
            else if (course.singleDeck)
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
            authoring.EditorAssign(definition, startPoint, writer.Zones[0], root);

            // Bays after the first, in the order the layout painted them.
            var extras = new ParkingZone[writer.Zones.Count - 1];

            for (int i = 1; i < writer.Zones.Count; i++)
            {
                extras[i - 1] = writer.Zones[i];
            }

            authoring.EditorAssignExtraBays(extras);
            EditorUtility.SetDirty(authoring);

            root.SetActive(false);

            Debug.Log($"[MissionCourseBuilder] Mission {course.missionId} '{course.name}' ({course.width:0}x{course.length:0}m, {definition.ParkingType}, {writer.Zones.Count} bay(s)) at {position:0.0} yaw {yaw:0}.", root);
        }

        // The thing each course's layout method writes into. Everything is in course-local
        // metres with +Z forward from the entrance, which is what lets the layouts below
        public class CourseWriter
        {
            // Light enough to be a road surface rather than a hole. At 0.21 it read as
            // black in the game, which is what a player called it: markings had nothing to
            // sit on and the lot had no depth.
            private static readonly Color Tarmac = new Color(0.30f, 0.30f, 0.33f);
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
            private int routes;
            private int parked;
            private int stripes;

            // What a BayRow plan is written out of.
            private const char EmptyBay = '.';
            private const char BayWithCar = '#';
            private const char TargetWithArrow = 'T';
            private const char TargetAnyWayRound = 't';
            private const char NoBay = ' ';

            private const float BayWidth = 3.3f;
            private const float BayLength = 6.4f;

            private static readonly MissionCourseKit.CarModel[] CarCycle =
            {
                MissionCourseKit.CarModel.Sedan,
                MissionCourseKit.CarModel.Hatchback,
                MissionCourseKit.CarModel.Muscle,
                MissionCourseKit.CarModel.Classic,
                MissionCourseKit.CarModel.HotRod
            };

            private readonly List<ParkingZone> zones = new List<ParkingZone>();

            public ParkingZone Zone => zones.Count > 0 ? zones[0] : null;
            public IReadOnlyList<ParkingZone> Zones => zones;
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
            public void Rail(float x, float z, float width, float length, float entranceGap = 0f,
                float entranceCentreX = 0f, float height = 1.6f,
                MissionCourseKit.Part part = MissionCourseKit.Part.Concrete)
            {
                float left = x - width * 0.5f;
                float right = x + width * 0.5f;
                float near = z - length * 0.5f;
                float far = z + length * 0.5f;

                kit.Wall(root, new Vector2(left, near), new Vector2(left, far), height, 0.5f, part);
                kit.Wall(root, new Vector2(right, near), new Vector2(right, far), height, 0.5f, part);
                kit.Wall(root, new Vector2(left, far), new Vector2(right, far), height, 0.5f, part);

                if (entranceGap <= 0f)
                {
                    kit.Wall(root, new Vector2(left, near), new Vector2(right, near), height, 0.5f, part);
                    return;
                }

                // The gap goes where the course's entrance actually is. Centring it blindly
                // walled the start point in on every course that begins off to one side.
                float gapLeft = Mathf.Clamp(entranceCentreX - entranceGap * 0.5f, left, right);
                float gapRight = Mathf.Clamp(entranceCentreX + entranceGap * 0.5f, left, right);

                kit.Wall(root, new Vector2(left, near), new Vector2(gapLeft, near), height, 0.5f, part);
                kit.Wall(root, new Vector2(gapRight, near), new Vector2(right, near), height, 0.5f, part);
            }

            // The wall round a lot, laid as alternating red and white blocks instead of as
            // one long one.
            //
            // The striped barrier prefab is a single short section. Scaling one of them to
            // thirty-four metres stretches its stripes with it, and from the driving seat
            // the whole thing read as a plain grey kerb. Blocks keep their stripes the size
            // they were drawn, because each one is a stripe.
            public void StripedRail(float width, float length, float height = 1.15f)
            {
                const float Segment = 3f;
                const float Thickness = 0.5f;

                float x = width * 0.5f;

                Side(-x, 0f, x, 0f);
                Side(-x, length, x, length);
                Side(-x, 0f, -x, length);
                Side(x, 0f, x, length);

                void Side(float x1, float z1, float x2, float z2)
                {
                    var from = new Vector2(x1, z1);
                    var to = new Vector2(x2, z2);

                    float run = (to - from).magnitude;
                    int count = Mathf.Max(1, Mathf.RoundToInt(run / Segment));
                    float each = run / count;

                    Vector2 step = (to - from) / count;
                    float yaw = Mathf.Atan2(step.x, step.y) * Mathf.Rad2Deg;

                    for (int i = 0; i < count; i++)
                    {
                        Vector2 centre = from + step * (i + 0.5f);

                        GameObject block = kit.Spawn(MissionCourseKit.Part.Concrete, root,
                            new Vector3(centre.x, level + height * 0.5f, centre.y), yaw,
                            new Vector3(Thickness, height, each));

                        bool red = (stripes++ & 1) == 0;

                        MissionCourseKit.Tint(block,
                            red ? "CourseRailRed" : "CourseRailWhite",
                            red ? new Color(0.76f, 0.19f, 0.17f) : new Color(0.93f, 0.92f, 0.89f));
                    }
                }
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

            // A run of bays side by side, written as a picture of the row:
            //
            //   .  an empty bay, painted white
            //   #  a bay with a car already in it
            //   T  a bay to park in, nose the way the painted arrow points
            //   t  a bay to park in, either way round - painted without an arrow
            //      a space leaves a gap, with no bay painted at all
            //
            // The row runs along +X from x, every bay facing yaw.
            public void BayRow(float x, float z, float yaw, string plan, float spacing = 3.45f)
            {
                PlaceBays(x, z, spacing, 0f, yaw, plan);
            }

            // The same row turned ninety degrees: bays stacked up the lot rather than
            // across it, which is how the long walls of a narrow lot are lined.
            public void BayColumn(float x, float z, float yaw, string plan, float spacing = 3.45f)
            {
                PlaceBays(x, z, 0f, spacing, yaw, plan);
            }

            // Bays set round a circle, each one square on to the middle of it. Reads as a
            // ring of parking round a fountain, which a grid cannot do.
            public void BayRing(float centreX, float centreZ, float radius, string plan, bool noseIn = true)
            {
                if (string.IsNullOrEmpty(plan))
                {
                    return;
                }

                for (int i = 0; i < plan.Length; i++)
                {
                    float angle = i * 360f / plan.Length;
                    float radians = angle * Mathf.Deg2Rad;

                    float x = centreX + Mathf.Sin(radians) * radius;
                    float z = centreZ + Mathf.Cos(radians) * radius;

                    // Pointing at the middle, or out of it.
                    float yaw = noseIn ? angle + 180f : angle;

                    PlaceBay(plan[i], x, z, yaw);
                }
            }

            private void PlaceBays(float x, float z, float stepX, float stepZ, float yaw, string plan)
            {
                if (string.IsNullOrEmpty(plan))
                {
                    return;
                }

                for (int i = 0; i < plan.Length; i++)
                {
                    PlaceBay(plan[i], x + i * stepX, z + i * stepZ, yaw);
                }
            }

            private void PlaceBay(char what, float x, float z, float yaw)
            {
                switch (what)
                {
                    case TargetWithArrow:
                        Bay(x, z, yaw, true, BayWidth, BayLength);
                        break;

                    case TargetAnyWayRound:
                        Bay(x, z, yaw, false, BayWidth, BayLength);
                        break;

                    case BayWithCar:
                        MarkBay(x, z, yaw);
                        Car(x, z, yaw, NextCar());
                        break;

                    case EmptyBay:
                        MarkBay(x, z, yaw);
                        break;

                    case NoBay:
                        break;

                    default:
                        Debug.LogWarning($"[MissionCourseBuilder] Mission {course.missionId}: {what} is not a bay.");
                        break;
                }
            }

            // White paint and nothing else: a bay that is part of the lot but not part of
            // the task.
            public void MarkBay(float x, float z, float yaw, float width = BayWidth, float length = BayLength)
            {
                var marking = new GameObject("BayMarking");
                marking.transform.SetParent(root, false);
                marking.transform.localPosition = new Vector3(x, level, z);
                marking.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

                kit.PaintBay(marking.transform, width, length, false, false);
            }

            // The centrepiece a lot is laid out around: a low hexagonal basin with water in
            // it. Built from three crossed boxes, because the project has no round mesh and
            // three boxes at sixty degrees read as a hexagon from the driving camera.
            public void Fountain(float x, float z, float radius)
            {
                for (int i = 0; i < 3; i++)
                {
                    float yaw = i * 60f;

                    GameObject wall = kit.Spawn(MissionCourseKit.Part.Concrete, root,
                        new Vector3(x, level + 0.3f, z), yaw, new Vector3(radius * 2f, 0.6f, radius * 1.16f));

                    MissionCourseKit.Tint(wall, "CourseFountainStone", new Color(0.62f, 0.58f, 0.52f));

                    // The water, sat just below the rim.
                    GameObject water = kit.Spawn(MissionCourseKit.Part.Plate, root,
                        new Vector3(x, level + 0.52f, z), yaw,
                        new Vector3(radius * 1.6f, 0.06f, radius * 0.93f));

                    MissionCourseKit.Tint(water, "CourseFountainWater", new Color(0.25f, 0.52f, 0.78f), true);
                }
            }

            // A line of the white pedestrian barriers, which is what a lot uses to close
            // off a section rather than a wall.
            public void WhiteBarriers(float x1, float z1, float x2, float z2, int count)
            {
                kit.BarrierLine(root, new Vector2(x1, z1), new Vector2(x2, z2), count,
                    MissionCourseKit.Part.PedestrianBarrier);
            }

            // Planting, as a low green block. The project has no hedge model, and a run of
            // these reads as one from the car.
            public void Hedge(float x, float z, float yaw, float length)
            {
                GameObject hedge = kit.Spawn(MissionCourseKit.Part.Concrete, root,
                    new Vector3(x, level + 0.45f, z), yaw, new Vector3(1.1f, 0.9f, length));

                MissionCourseKit.Tint(hedge, "CourseHedge", new Color(0.28f, 0.52f, 0.26f));
            }

            // Walks the car models, so a full row is not ten of the same car.
            private MissionCourseKit.CarModel NextCar()
            {
                return CarCycle[parked++ % CarCycle.Length];
            }

            // A broken white line down the middle of a driving lane.
            public void LaneLine(float x1, float z1, float x2, float z2, bool dashed = true)
            {
                kit.PaintLine(root, new Vector3(x1, level, z1), new Vector3(x2, level, z2), dashed);
            }

            // A block of city round the lot: a ring road hard against the wall, buildings
            // packed shoulder to shoulder outside it, a light on each corner and cars going
            // round.
            //
            // Built into the course rather than across the site. Only one course is ever
            // switched on, so only one block of city is ever loaded - and a ring that hugs
            // the lot is what a player sees over a 1.15m wall, where a city scattered over
            // four hundred metres was mostly out of sight and the rest of it floating.
            public void Surround()
            {
                const float Verge = 1.5f;
                const float RoadWidth = 8f;
                const float Pavement = 2.5f;

                float half = course.width * 0.5f;
                float mid = RoadWidth * 0.5f;

                // Centre line of the ring, which is what the road is drawn on, the cars
                // drive along and the buildings stand back from.
                float ringX = half + Verge + mid;
                float ringNear = -(Verge + mid);
                float ringFar = course.length + Verge + mid;

                Road(0f, ringNear, course.width + (Verge + RoadWidth) * 2f, RoadWidth);
                Road(0f, ringFar, course.width + (Verge + RoadWidth) * 2f, RoadWidth);
                Road(-ringX, course.length * 0.5f, RoadWidth, course.length + (Verge + RoadWidth) * 2f);
                Road(ringX, course.length * 0.5f, RoadWidth, course.length + (Verge + RoadWidth) * 2f);

                float blockX = ringX + mid + Pavement;
                float blockNear = ringNear - mid - Pavement;
                float blockFar = ringFar + mid + Pavement;

                Buildings(-blockX, blockNear, -blockX, blockFar, 90f);
                Buildings(blockX, blockNear, blockX, blockFar, 270f);
                Buildings(-blockX, blockNear, blockX, blockNear, 0f);
                Buildings(-blockX, blockFar, blockX, blockFar, 180f);

                RingTraffic(ringX, ringNear, ringFar);
            }

            private void Road(float x, float z, float width, float length)
            {
                GameObject slab = kit.Spawn(MissionCourseKit.Part.Concrete, root,
                    new Vector3(x, level + 0.03f, z), 0f, new Vector3(width, 0.06f, length));

                MissionCourseKit.Tint(slab, "SiteRoad", new Color(0.24f, 0.24f, 0.26f));

                // A line down the middle, the long way. Without one the ring is a dark band
                // against dark tarmac and reads as nothing at all.
                bool alongX = width > length;

                Vector3 from = alongX
                    ? new Vector3(x - width * 0.5f + 3f, level + 0.06f, z)
                    : new Vector3(x, level + 0.06f, z - length * 0.5f + 3f);

                Vector3 to = alongX
                    ? new Vector3(x + width * 0.5f - 3f, level + 0.06f, z)
                    : new Vector3(x, level + 0.06f, z + length * 0.5f - 3f);

                kit.PaintLine(root, from, to, true, 0.2f, 2.6f, 2.6f);
            }

            // Shoulder to shoulder along one side, facing the road.
            private void Buildings(float x1, float z1, float x2, float z2, float facing)
            {
                List<CityChunkLibrary.Building> stock = Stock();

                if (stock.Count == 0)
                {
                    return;
                }

                var from = new Vector2(x1, z1);
                var to = new Vector2(x2, z2);

                float run = (to - from).magnitude;
                Vector2 step = (to - from) / run;

                float along = 0f;

                while (along < run)
                {
                    CityChunkLibrary.Building building = stock[surroundRandom.Next(stock.Count)];

                    // Turned to face the road, so its front is what the player sees. Its
                    // width along the row is then its own depth or breadth depending on the
                    // turn, so the step is taken from the turned footprint.
                    float width = Mathf.Abs(facing % 180f) < 1f ? building.size.x : building.size.z;
                    width = Mathf.Max(6f, width);

                    if (along + width > run + width * 0.5f)
                    {
                        break;
                    }

                    Vector2 at = from + step * (along + width * 0.5f);

                    var block = new GameObject("Building");
                    block.transform.SetParent(root, false);
                    block.transform.localPosition = new Vector3(at.x, level, at.y);
                    block.transform.localRotation = Quaternion.Euler(0f, facing, 0f);

                    block.AddComponent<MeshFilter>().sharedMesh = building.mesh;

                    var renderer = block.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = building.materials;

                    // Backdrop: a skyline the player cannot reach is not worth shadowing,
                    // and it has no collider because nothing can get to it anyway.
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;

                    along += width + 0.6f;
                }
            }

            // Cars going round the ring, with a light on each corner.
            private void RingTraffic(float ringX, float ringNear, float ringFar)
            {
                var host = new GameObject("RingTraffic");
                host.transform.SetParent(root, false);

                var path = host.AddComponent<CarParkingGame.Traffic.WaypointPath>();
                var points = new List<Transform>();

                var corners = new[]
                {
                    new Vector2(-ringX, ringNear),
                    new Vector2(ringX, ringNear),
                    new Vector2(ringX, ringFar),
                    new Vector2(-ringX, ringFar)
                };

                var lights = new List<CarParkingGame.Traffic.TrafficLight>();
                var lightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TrafficLightGenerator.PrefabPath);

                for (int i = 0; i < corners.Length; i++)
                {
                    Vector2 from = corners[i];
                    Vector2 to = corners[(i + 1) % corners.Length];

                    int steps = Mathf.Max(2, Mathf.CeilToInt((to - from).magnitude / 10f));

                    for (int step = 0; step < steps; step++)
                    {
                        Vector2 at = Vector2.Lerp(from, to, step / (float)steps);

                        var point = new GameObject($"Waypoint {points.Count:00}");
                        point.transform.SetParent(host.transform, false);
                        point.transform.localPosition = new Vector3(at.x, level, at.y);

                        var data = point.AddComponent<CarParkingGame.Traffic.TrafficWaypoint>();
                        points.Add(point.transform);

                        // A light on the approach to each corner, and the two pairs across
                        // from each other share a phase so one way is moving while the
                        // other waits.
                        if (step == steps - 2 && lightPrefab != null)
                        {
                            var head = (GameObject)PrefabUtility.InstantiatePrefab(lightPrefab, host.transform);
                            head.transform.localPosition = point.transform.localPosition;
                            head.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);

                            var light = head.GetComponent<CarParkingGame.Traffic.TrafficLight>();
                            lights.Add(light);

                            var waypoint = new SerializedObject(data);
                            waypoint.FindProperty("governingLight").objectReferenceValue = light;
                            waypoint.FindProperty("isCrossing").boolValue = true;
                            waypoint.ApplyModifiedProperties();
                        }
                    }
                }

                var serialized = new SerializedObject(path);
                SerializedProperty array = serialized.FindProperty("waypoints");
                array.arraySize = points.Count;

                for (int i = 0; i < points.Count; i++)
                {
                    array.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
                }

                serialized.FindProperty("loop").boolValue = true;
                serialized.ApplyModifiedProperties();

                Lights(host.transform, lights);
                Cars(host.transform, path);
            }

            private void Lights(Transform host, List<CarParkingGame.Traffic.TrafficLight> lights)
            {
                if (lights.Count == 0)
                {
                    return;
                }

                var group = host.gameObject.AddComponent<CarParkingGame.Traffic.TrafficLightGroup>();
                var serialized = new SerializedObject(group);
                SerializedProperty phases = serialized.FindProperty("phases");

                phases.arraySize = 2;

                for (int phase = 0; phase < 2; phase++)
                {
                    SerializedProperty entry = phases.GetArrayElementAtIndex(phase);
                    entry.FindPropertyRelative("name").stringValue = phase == 0 ? "Across" : "Along";

                    SerializedProperty array = entry.FindPropertyRelative("lights");
                    var mine = new List<CarParkingGame.Traffic.TrafficLight>();

                    for (int i = phase; i < lights.Count; i += 2)
                    {
                        mine.Add(lights[i]);
                    }

                    array.arraySize = mine.Count;

                    for (int i = 0; i < mine.Count; i++)
                    {
                        array.GetArrayElementAtIndex(i).objectReferenceValue = mine[i];
                    }
                }

                serialized.ApplyModifiedProperties();
            }

            private void Cars(Transform host, CarParkingGame.Traffic.WaypointPath path)
            {
                var prefabs = new List<CarParkingGame.Traffic.TrafficVehicle>();

                foreach (string name in new[] { "Traffic_SEDAN", "Traffic_HATCHBACK_1988", "Traffic_ClassicCarFull" })
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        $"Assets/GameAssets/Prefabs/Traffic/{name}.prefab");

                    if (prefab != null && prefab.GetComponent<CarParkingGame.Traffic.TrafficVehicle>() != null)
                    {
                        prefabs.Add(prefab.GetComponent<CarParkingGame.Traffic.TrafficVehicle>());
                    }
                }

                if (prefabs.Count == 0)
                {
                    return;
                }

                var pool = new GameObject("Pool");
                pool.transform.SetParent(host, false);

                var manager = host.gameObject.AddComponent<CarParkingGame.Traffic.TrafficManager>();
                var serialized = new SerializedObject(manager);

                SerializedProperty cars = serialized.FindProperty("vehiclePrefabs");
                cars.arraySize = prefabs.Count;

                for (int i = 0; i < prefabs.Count; i++)
                {
                    cars.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
                }

                SerializedProperty paths = serialized.FindProperty("paths");
                paths.arraySize = 1;
                paths.GetArrayElementAtIndex(0).objectReferenceValue = path;

                // A short ring holds few cars before they are nose to tail.
                serialized.FindProperty("maximumVehicles").intValue = 6;
                serialized.FindProperty("poolParent").objectReferenceValue = pool.transform;
                serialized.ApplyModifiedProperties();
            }

            private static List<CityChunkLibrary.Building> cachedStock;
            private readonly System.Random surroundRandom = new System.Random(20261006);

            private static List<CityChunkLibrary.Building> Stock()
            {
                return cachedStock ??= CityChunkLibrary.Load();
            }

            public void Arrow(float x, float z, float yaw)
            {
                kit.PaintArrow(root, new Vector3(x, level, z), yaw);
            }

            // Records a path the car has to be able to drive down, as empty transforms the
            // scene keeps. The course checker sweeps a car along it afterwards.
            //
            // The checker reloads the scene and has no idea how a course was drawn, so it
            // cannot tell a corridor from open tarmac. Without this it could only check
            // that the start and the bay were clear, and it passed every one of the
            // courses whose lane was walled shut at a corner.
            public void Route(params Vector2[] path)
            {
                if (path == null || path.Length < 2)
                {
                    return;
                }

                var route = new GameObject("Route " + ++routes);
                Undo.RegisterCreatedObjectUndo(route, "Record route");

                route.transform.SetParent(root, false);

                for (int i = 0; i < path.Length; i++)
                {
                    var point = new GameObject("Point " + i);
                    point.transform.SetParent(route.transform, false);
                    point.transform.localPosition = new Vector3(path[i].x, level, path[i].y);
                }
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
                Bay(x, z, yaw, true, width, length);
            }

            // A bay the player has to fill. Painted yellow, and with an arrow only when it
            // has to be entered a particular way round: the paint and the heading check
            // come from the same flag so they cannot disagree. An arrow the validator
            // ignores, or a heading check with nothing on the ground to say which way, are
            // each just a level the player cannot read.
            //
            // Bays are filled in the order they are written, and that order is what the
            // P 1/3 counter counts.
            public void Bay(float x, float z, float yaw, bool requireHeading,
                float width = 3.2f, float length = 6.5f)
            {
                var bayObject = new GameObject("ParkingBay " + (zones.Count + 1));
                bayObject.transform.SetParent(root, false);
                bayObject.transform.localPosition = new Vector3(x, level, z);
                bayObject.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

                var zone = bayObject.AddComponent<ParkingZone>();
                zone.EditorSetBox(Vector3.zero, new Vector3(width, 2.5f, length));
                zone.EditorSetParkedFacingBackward(false);
                zone.EditorSetRequireHeading(requireHeading);

                kit.PaintBay(bayObject.transform, width, length, true, requireHeading);

                zones.Add(zone);
            }
        }
    }
}
