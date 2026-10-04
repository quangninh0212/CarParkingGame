using System.Collections.Generic;
using CarParkingGame.Missions;
using CarParkingGame.Parking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarParkingGame.EditorTools
{
    // Checks every mission is actually playable, with the physics engine rather than by
    // eye.
    //
    // Looking at courses from above found the obvious faults and missed the ones that
    // matter: a course with no floor looks identical to one with a floor, a bay with a
    // pillar standing in it looks fine until you try to park there, and a lower deck
    // roofed over by its own course reads perfectly from directly overhead.
    //
    // Read-only.
    public static class MissionCourseCheck
    {
        private const string ScenePath = "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity";

        // Roughly the car: 1.95m wide, 5m long, and about 1.2m of body above the road.
        private static readonly Vector3 CarHalfExtents = new Vector3(0.98f, 0.6f, 2.5f);

        private const float RideHeight = 0.25f;

        [MenuItem("Tools/Car Parking/Check Mission Courses")]
        public static void RunInOpenScene()
        {
            Report(Check());
        }

        public static void RunFromCommandLine()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError($"[MissionCourseCheck] Could not open '{ScenePath}'.");
                EditorApplication.Exit(1);
                return;
            }

            List<string> problems = Check();
            Report(problems);
            EditorApplication.Exit(problems.Count == 0 ? 0 : 1);
        }

        private static void Report(List<string> problems)
        {
            if (problems.Count == 0)
            {
                Debug.Log("[MissionCourseCheck] Every mission has a floor, a clear bay, a clear start and a way between them.");
                return;
            }

            Debug.LogError($"[MissionCourseCheck] {problems.Count} problem(s):");

            foreach (string problem in problems)
            {
                Debug.LogError("  - " + problem);
            }
        }

        private static List<string> Check()
        {
            var problems = new List<string>();
            var manager = Object.FindFirstObjectByType<MissionManager>(FindObjectsInactive.Include);

            if (manager == null)
            {
                problems.Add("No MissionManager in the scene.");
                return problems;
            }

            manager.RebuildRegistry();

            foreach (MissionAuthoring mission in manager.RegisteredMissions)
            {
                mission.SetEnvironmentActive(true);
            }

            Physics.SyncTransforms();

            foreach (MissionAuthoring mission in manager.RegisteredMissions)
            {
                CheckMission(mission, problems);
            }

            return problems;
        }

        private static void CheckMission(MissionAuthoring mission, List<string> problems)
        {
            int id = mission.MissionId;
            ParkingZone zone = mission.ParkingZone;
            Transform start = mission.StartPoint;

            if (zone == null || start == null)
            {
                problems.Add($"Mission {id}: missing a parking zone or start point.");
                return;
            }

            Vector3 bay = zone.WorldCenter;
            Quaternion bayRotation = Quaternion.LookRotation(zone.ParkedHeading, Vector3.up);

            CheckStandable(id, "start point", start.position, start.rotation, problems);
            CheckStandable(id, "parking bay", bay + Vector3.up * 0.5f, bayRotation, problems);

            // Nothing overhead. A deck built over its own lower level looks right from
            // above and traps the car underneath it.
            CheckHeadroom(id, "start point", start.position, problems);
            CheckHeadroom(id, "parking bay", bay, problems);

            // Ground the whole way from the start to the bay. This is a straight line, not
            // the route - a course with corners will have walls across it - so only a
            // missing floor is reported, not an obstruction.
            CheckFloorAlong(id, start.position, bay, problems);

            // And, where the course recorded the lane it wants driven, that a car actually
            // fits down it.
            CheckRoutes(mission, problems);
        }

        // Sweeps a car-width box along every route the course recorded.
        //
        // A corridor on an open pad cannot be checked by asking whether the bay is
        // reachable: walk round the outside of a walled lane and it is. Checking the start
        // and the bay alone passed four courses whose lane was sealed at a corner by its
        // own wall, which is how two of them reached a player.
        //
        // The box is as wide as the car but only as long as it is wide. A full 5m car
        // cannot be swung round a corner in a check like this without false alarms, and a
        // short box is still far too big to pass through a wall - which is the fault being
        // looked for.
        private static void CheckRoutes(MissionAuthoring mission, List<string> problems)
        {
            var span = new Vector3(CarHalfExtents.x, CarHalfExtents.y, CarHalfExtents.x);

            foreach (Transform route in mission.GetComponentsInChildren<Transform>(true))
            {
                if (!route.name.StartsWith("Route ") || route.childCount < 2)
                {
                    continue;
                }

                for (int leg = 0; leg < route.childCount - 1; leg++)
                {
                    Vector3 from = route.GetChild(leg).position;
                    Vector3 to = route.GetChild(leg + 1).position;

                    if (CheckLeg(mission.MissionId, route.name, from, to, span, problems))
                    {
                        return;
                    }
                }
            }
        }

        private static bool CheckLeg(int id, string route, Vector3 from, Vector3 to, Vector3 span,
            List<string> problems)
        {
            Vector3 along = to - from;
            along.y = 0f;

            if (along.sqrMagnitude < 0.01f)
            {
                return false;
            }

            Quaternion facing = Quaternion.LookRotation(along.normalized, Vector3.up);
            int steps = Mathf.Max(2, Mathf.CeilToInt(along.magnitude / 0.5f));

            for (int i = 0; i <= steps; i++)
            {
                Vector3 at = Vector3.Lerp(from, to, i / (float)steps);

                if (!Physics.Raycast(at + Vector3.up * 3f, Vector3.down, out RaycastHit floor, 16f))
                {
                    problems.Add($"Mission {id}: no floor under {route} at {at:0.0}.");
                    return true;
                }

                Vector3 centre = floor.point + Vector3.up * (RideHeight + CarHalfExtents.y);

                foreach (Collider collider in Physics.OverlapBox(centre, span, facing))
                {
                    if (collider.isTrigger || collider == floor.collider)
                    {
                        continue;
                    }

                    problems.Add($"Mission {id}: '{collider.name}' blocks {route} at {at:0.0} - the lane is sealed and the car cannot get through.");
                    return true;
                }
            }

            return false;
        }

        // Somewhere the car can physically be: floor underneath, and nothing solid filling
        // the space it would occupy.
        private static void CheckStandable(int id, string what, Vector3 at, Quaternion rotation, List<string> problems)
        {
            if (!Physics.Raycast(at + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 14f))
            {
                problems.Add($"Mission {id}: nothing solid under the {what} - the car falls through the world.");
                return;
            }

            Vector3 centre = hit.point + Vector3.up * (RideHeight + CarHalfExtents.y);

            foreach (Collider collider in Physics.OverlapBox(centre, CarHalfExtents, rotation))
            {
                if (collider.isTrigger || IsFloorOf(collider, hit.collider))
                {
                    continue;
                }

                problems.Add($"Mission {id}: '{collider.name}' at {collider.bounds.center:0.0} (size {collider.bounds.size:0.0}) is standing in the {what} at {at:0.0}.");
                return;
            }
        }

        private static void CheckHeadroom(int id, string what, Vector3 at, List<string> problems)
        {
            if (!Physics.Raycast(at + Vector3.up * 2f, Vector3.down, out RaycastHit floor, 14f))
            {
                return;
            }

            if (Physics.Raycast(floor.point + Vector3.up * 0.3f, Vector3.up, out RaycastHit above, 5f)
                && !above.collider.isTrigger)
            {
                problems.Add($"Mission {id}: the {what} is roofed over by '{above.collider.name}' {above.distance:0.0}m up.");
            }
        }

        private static void CheckFloorAlong(int id, Vector3 from, Vector3 to, List<string> problems)
        {
            const int Steps = 24;

            for (int i = 0; i <= Steps; i++)
            {
                Vector3 at = Vector3.Lerp(from, to, i / (float)Steps);

                if (!Physics.Raycast(at + Vector3.up * 6f, Vector3.down, 30f))
                {
                    problems.Add($"Mission {id}: a hole in the floor between the start and the bay, {i * 100 / Steps}% of the way along.");
                    return;
                }
            }
        }

        // The surface the car is standing on is not an obstruction, and neither is the
        // slab it is part of.
        private static bool IsFloorOf(Collider candidate, Collider floor)
        {
            return candidate == floor;
        }
    }
}
