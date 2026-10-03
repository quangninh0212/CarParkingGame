using System;
using System.Collections.Generic;
using CarParkingGame.Missions;
using CarParkingGame.Parking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarParkingGame.EditorTools
{
    // Builds missions 9-30 as real, distinct places on the track.
    //
    // They used to be clones of missions 1-8 standing at exactly the same world positions,
    // which worked only because one mission's props were ever active at a time. That made
    // twenty-two "levels" that are the same eight car parks, and it ruled out a challenge
    // run that visits more than eight bays.
    //
    // Sites are not invented. The ground is sampled on a grid, every sample is raycast
    // down, and a sample is only kept when it lands on a surface the car can actually
    // drive on - the tarmac, pit lane and concrete meshes, by name. Each kept sample is
    // then tested in four headings with two box casts: one for the bay, one for the
    // approach lane the car has to come down. Anything that is not ground has to be clear
    // of both, which is only meaningful because the scenery now has colliders.
    //
    // Run MissionSiteShotTool afterwards: it renders each site from above, which is the
    // only way to see that a bay landed somewhere sensible.
    public static class MissionSiteBuilder
    {
        private const string ScenePath = "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity";
        private const string CatalogPath = "Assets/GameAssets/ScriptableObjects/MissionCatalog.asset";

        private const int FirstGeneratedMission = 9;
        private const int LastGeneratedMission = 30;

        // The surfaces a car may stand on. Grass, gravel and sand are deliberately absent:
        // a parking bay in a gravel trap is not a parking bay.
        private static readonly HashSet<string> DrivableSurfaces = new HashSet<string>
        {
            "1TARMAC_back",
            "1TARMAC_inner",
            "1TARMAC_oval",
            "1TARMACgarages",
            "1PITLANE",
            "1CONCRETE_red"
        };

        private const float SampleStep = 5f;
        private const float BayLength = 6.5f;
        private const float ApproachLength = 20f;
        private const float LaneWidth = 4.2f;
        private const float MinimumSiteSpacing = 40f;
        private const float Clearance = 0.6f;

        [MenuItem("Tools/Car Parking/Build Distinct Mission Sites 9-30")]
        public static void RunInOpenScene()
        {
            Build();
        }

        public static void RunFromCommandLine()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError($"[MissionSiteBuilder] Could not open '{ScenePath}'.");
                EditorApplication.Exit(1);
                return;
            }

            bool ok = Build();

            if (ok)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[MissionSiteBuilder] Saved '{ScenePath}'.");
            }

            EditorApplication.Exit(ok ? 0 : 1);
        }

        private readonly struct Site
        {
            public readonly Vector3 position;
            public readonly Vector3 heading;

            public Site(Vector3 position, Vector3 heading)
            {
                this.position = position;
                this.heading = heading;
            }
        }

        private static bool Build()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MissionCatalog>(CatalogPath);

            if (catalog == null)
            {
                Debug.LogError($"[MissionSiteBuilder] No catalog at '{CatalogPath}'.");
                return false;
            }

            Physics.SyncTransforms();

            List<Vector3> existing = CollectExistingBayPositions(out GameObject propSource, out GameObject coneSource);

            if (propSource == null)
            {
                Debug.LogError("[MissionSiteBuilder] No barrier block in the scene to copy; cannot dress new bays.");
                return false;
            }

            // The props to copy usually live inside one of the containers this is about to
            // delete, so detached copies are taken first. Copying straight from the scene
            // object meant the template vanished halfway through the run.
            var templateRoot = new GameObject("~MissionSiteTemplates");
            GameObject propTemplate = Detach(propSource, templateRoot.transform);
            GameObject coneTemplate = Detach(coneSource, templateRoot.transform);

            List<Site> sites = FindSites(existing, LastGeneratedMission - FirstGeneratedMission + 1);

            Debug.Log($"[MissionSiteBuilder] Found {sites.Count} usable site(s) for {LastGeneratedMission - FirstGeneratedMission + 1} mission(s).");

            if (sites.Count == 0)
            {
                Debug.LogError("[MissionSiteBuilder] No usable ground found. Has Make Track Scenery Solid been run?");
                return false;
            }

            int built = 0;

            for (int missionId = FirstGeneratedMission; missionId <= LastGeneratedMission; missionId++)
            {
                MissionDefinition definition = catalog.Find(missionId);

                if (definition == null)
                {
                    Debug.LogWarning($"[MissionSiteBuilder] No definition for mission {missionId}; skipped.");
                    continue;
                }

                int index = missionId - FirstGeneratedMission;

                if (index >= sites.Count)
                {
                    Debug.LogWarning($"[MissionSiteBuilder] Ran out of ground at mission {missionId}; it keeps its old layout.");
                    continue;
                }

                ReplaceExisting(missionId);
                BuildSite(sites[index], definition, propTemplate, coneTemplate);
                built++;
            }

            UnityEngine.Object.DestroyImmediate(templateRoot);

            Debug.Log($"[MissionSiteBuilder] Built {built} distinct mission site(s).");
            return built > 0;
        }

        private static GameObject Detach(GameObject source, Transform holder)
        {
            if (source == null)
            {
                return null;
            }

            GameObject copy = UnityEngine.Object.Instantiate(source, holder);
            copy.name = source.name;
            copy.SetActive(false);
            return copy;
        }

        // ----- finding ground --------------------------------------------------------------

        private static List<Site> FindSites(List<Vector3> existing, int wanted)
        {
            Bounds area = MeasureDrivableArea(out bool measured);

            if (!measured)
            {
                Debug.LogError("[MissionSiteBuilder] Could not find the track's drivable meshes.");
                return new List<Site>();
            }

            var taken = new List<Vector3>(existing);
            var found = new List<Site>();

            // Headings are axis-aligned plus the diagonals: the track's car parks and pit
            // boxes are laid out on those, and a bay at an arbitrary angle reads as a
            // mistake even when the ground under it is clear.
            var headings = new Vector3[8];

            for (int i = 0; i < headings.Length; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                headings[i] = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            }

            var candidates = new List<Site>();

            for (float x = area.min.x; x <= area.max.x; x += SampleStep)
            {
                for (float z = area.min.z; z <= area.max.z; z += SampleStep)
                {
                    if (!TryGroundAt(new Vector2(x, z), out Vector3 ground))
                    {
                        continue;
                    }

                    foreach (Vector3 heading in headings)
                    {
                        if (IsSiteClear(ground, heading))
                        {
                            candidates.Add(new Site(ground, heading));
                            break;
                        }
                    }
                }
            }

            Debug.Log($"[MissionSiteBuilder] {candidates.Count} clear candidate(s) before spacing.");

            // Spread them out: taking candidates in order would cluster them all in the
            // first car park the sweep walks across.
            foreach (Site candidate in Shuffle(candidates))
            {
                if (found.Count >= wanted)
                {
                    break;
                }

                if (IsTooClose(candidate.position, taken))
                {
                    continue;
                }

                found.Add(candidate);
                taken.Add(candidate.position);
            }

            return found;
        }

        // A fixed seed, so a rebuild puts the missions back in the same places and the
        // scene diff stays readable.
        private static IEnumerable<Site> Shuffle(List<Site> source)
        {
            var random = new System.Random(20260601);
            var copy = new List<Site>(source);

            for (int i = copy.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (copy[i], copy[j]) = (copy[j], copy[i]);
            }

            return copy;
        }

        private static bool IsTooClose(Vector3 position, List<Vector3> taken)
        {
            foreach (Vector3 other in taken)
            {
                if ((new Vector2(other.x, other.z) - new Vector2(position.x, position.z)).sqrMagnitude
                    < MinimumSiteSpacing * MinimumSiteSpacing)
                {
                    return true;
                }
            }

            return false;
        }

        private static Bounds MeasureDrivableArea(out bool measured)
        {
            var bounds = new Bounds();
            measured = false;

            foreach (MeshRenderer renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!DrivableSurfaces.Contains(renderer.name))
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

        private static bool TryGroundAt(Vector2 point, out Vector3 ground)
        {
            ground = default;

            var origin = new Vector3(point.x, 200f, point.y);

            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 400f))
            {
                return false;
            }

            if (!DrivableSurfaces.Contains(hit.collider.name))
            {
                return false;
            }

            // Flat ground only; a bay on a banked corner cannot be parked in.
            if (Vector3.Angle(hit.normal, Vector3.up) > 6f)
            {
                return false;
            }

            ground = hit.point;
            return true;
        }

        // The bay itself, and the lane the car comes down to reach it, both have to be
        // clear of anything that is not ground.
        private static bool IsSiteClear(Vector3 ground, Vector3 heading)
        {
            Quaternion rotation = Quaternion.LookRotation(heading, Vector3.up);

            if (!IsBoxClear(ground, rotation, new Vector3(LaneWidth, 3f, BayLength)))
            {
                return false;
            }

            Vector3 approachCentre = ground - heading * (BayLength * 0.5f + ApproachLength * 0.5f);

            if (!IsBoxClear(approachCentre, rotation, new Vector3(LaneWidth, 3f, ApproachLength)))
            {
                return false;
            }

            // The whole lane has to be on drivable ground too, not just clear of objects:
            // a clear box can still be hanging over grass.
            for (float t = -ApproachLength; t <= BayLength * 0.5f; t += 4f)
            {
                Vector3 along = ground + heading * t;

                if (!TryGroundAt(new Vector2(along.x, along.z), out Vector3 _))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsBoxClear(Vector3 centre, Quaternion rotation, Vector3 size)
        {
            // Lifted clear of the road surface so the ground itself is not an obstacle.
            Vector3 position = centre + Vector3.up * (size.y * 0.5f + Clearance);

            foreach (Collider hit in Physics.OverlapBox(position, size * 0.5f, rotation))
            {
                if (hit.isTrigger)
                {
                    continue;
                }

                if (DrivableSurfaces.Contains(hit.name))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        // ----- building ----------------------------------------------------------------------

        private static List<Vector3> CollectExistingBayPositions(out GameObject propTemplate, out GameObject coneTemplate)
        {
            propTemplate = null;
            coneTemplate = null;

            var positions = new List<Vector3>();

            foreach (MissionAuthoring authoring in UnityEngine.Object.FindObjectsByType<MissionAuthoring>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (authoring.MissionId < FirstGeneratedMission && authoring.ParkingZone != null)
                {
                    positions.Add(authoring.ParkingZone.WorldCenter);
                }
            }

            foreach (MeshFilter filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (propTemplate == null && filter.name.StartsWith("Block_Barrier_1_", StringComparison.Ordinal))
                {
                    propTemplate = filter.gameObject;
                }

                if (coneTemplate == null && filter.name.StartsWith("Cone_Clean", StringComparison.Ordinal))
                {
                    coneTemplate = filter.gameObject;
                }
            }

            return positions;
        }

        private static void ReplaceExisting(int missionId)
        {
            foreach (MissionAuthoring authoring in UnityEngine.Object.FindObjectsByType<MissionAuthoring>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (authoring.MissionId == missionId)
                {
                    UnityEngine.Object.DestroyImmediate(authoring.gameObject);
                    return;
                }
            }
        }

        private static void BuildSite(Site site, MissionDefinition definition, GameObject propTemplate, GameObject coneTemplate)
        {
            int missionId = definition.MissionId;

            var root = new GameObject($"Mission{missionId:00}_Site");
            Undo.RegisterCreatedObjectUndo(root, "Build mission site");

            Quaternion rotation = Quaternion.LookRotation(site.heading, Vector3.up);
            root.transform.SetPositionAndRotation(site.position, rotation);

            // Harder missions get a tighter bay. The floor is the width of the widest car
            // plus a hand's width either side; below that the mission is not hard, it is
            // impossible.
            float width = Mathf.Lerp(3.4f, 2.7f, Mathf.InverseLerp(1, 5, definition.Difficulty));
            bool parallel = definition.ParkingType == ParkingType.Parallel;

            // A parallel bay is longer, not turned: the car still ends up facing along the
            // lane. What makes it parallel parking is that it is entered from the side,
            // with a car ahead and a car behind, which is what DressBay lays out.
            var baySize = new Vector3(width, 2.5f, parallel ? BayLength + 1.5f : BayLength);

            var zoneObject = new GameObject("ParkingZone");
            zoneObject.transform.SetParent(root.transform, false);

            var zone = zoneObject.AddComponent<ParkingZone>();
            zone.EditorSetBox(Vector3.zero, baySize);
            zone.EditorSetParkedFacingBackward(false);

            Transform startPoint = BuildStartPoint(root.transform, definition);
            DressBay(root.transform, baySize, propTemplate, coneTemplate, definition);

            var authoring = root.AddComponent<MissionAuthoring>();
            authoring.EditorAssign(definition, startPoint, zone, root);
            EditorUtility.SetDirty(authoring);

            root.SetActive(false);

            Debug.Log($"[MissionSiteBuilder] Mission {missionId} ('{definition.DisplayName}', {definition.ParkingType}) at {site.position:0.0} facing {site.heading:0.00}.", root);
        }

        // Where the car is dropped, which is what decides the manoeuvre:
        //  - forward: straight down the lane at the bay,
        //  - reverse: past the bay facing away, so the only way in is backwards, which is
        //    what the validator insists on before it will accept the park,
        //  - parallel: alongside and short of the bay, so it has to be entered from the side.
        private static Transform BuildStartPoint(Transform root, MissionDefinition definition)
        {
            var startPoint = new GameObject("StartPoint");
            startPoint.transform.SetParent(root, false);

            startPoint.transform.localPosition = definition.ParkingType switch
            {
                ParkingType.Reverse => new Vector3(0f, 1f, 12f),
                ParkingType.Parallel => new Vector3(-3.6f, 1f, -11f),
                _ => new Vector3(0f, 1f, -15f)
            };

            startPoint.transform.localRotation = Quaternion.identity;
            return startPoint.transform;
        }

        private static void DressBay(
            Transform root,
            Vector3 baySize,
            GameObject propTemplate,
            GameObject coneTemplate,
            MissionDefinition definition)
        {
            float halfWidth = baySize.x * 0.5f;
            float halfLength = baySize.z * 0.5f;

            if (definition.ParkingType == ParkingType.Parallel)
            {
                // Blocked fore and aft and open on both sides: a kerbside gap between two
                // parked cars, which is the whole point of parallel parking.
                Clone(propTemplate, root, new Vector3(0f, 0f, halfLength + 0.7f), Quaternion.Euler(0f, 90f, 0f), "CarAhead");
                Clone(propTemplate, root, new Vector3(0f, 0f, -halfLength - 0.7f), Quaternion.Euler(0f, 90f, 0f), "CarBehind");
                return;
            }

            // Blocks down both sides of the bay and one across the back, so the bay reads
            // as a bay from the driving seat rather than as an invisible rectangle.
            for (int i = 0; i < 4; i++)
            {
                float z = Mathf.Lerp(-halfLength, halfLength, i / 3f);

                Clone(propTemplate, root, new Vector3(-halfWidth - 0.35f, 0f, z), Quaternion.identity, "SideLeft" + i);
                Clone(propTemplate, root, new Vector3(halfWidth + 0.35f, 0f, z), Quaternion.identity, "SideRight" + i);
            }

            Clone(propTemplate, root, new Vector3(0f, 0f, halfLength + 0.6f), Quaternion.Euler(0f, 90f, 0f), "BackStop");

            if (coneTemplate == null || definition.Difficulty < 3)
            {
                return;
            }

            // Cones narrow the approach on the harder missions.
            int cones = 3 + definition.Difficulty;
            float gate = Mathf.Lerp(3.2f, 2.4f, Mathf.InverseLerp(3, 5, definition.Difficulty));

            for (int i = 0; i < cones; i++)
            {
                float z = -halfLength - 2f - i * 2.4f;

                Clone(coneTemplate, root, new Vector3(-gate, 0f, z), Quaternion.identity, $"ConeLeft{i}");
                Clone(coneTemplate, root, new Vector3(gate, 0f, z), Quaternion.identity, $"ConeRight{i}");
            }
        }

        private static void Clone(GameObject template, Transform parent, Vector3 localPosition, Quaternion localRotation, string name)
        {
            GameObject copy = UnityEngine.Object.Instantiate(template, parent);
            copy.name = name;
            copy.transform.localPosition = localPosition;
            copy.transform.localRotation = localRotation;
            copy.transform.localScale = template.transform.localScale;
            copy.SetActive(true);
        }
    }
}
