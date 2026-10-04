using CarParkingGame.Missions;
using CarParkingGame.Parking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarParkingGame.EditorTools
{
    // Paints the original eight missions' bays to match the authored courses: a closed
    // yellow rectangle with an arrow pointing the way the car's nose has to end up.
    //
    // Those bays are the legacy ParkingTrigger plates, which have no markings of their own
    // and are scaled to (1, 2.3, 0.19) - so the paint cannot be parented to them, or it
    // would be squashed by the same factor. It goes on its own object instead, placed at
    // the zone's measured centre.
    //
    // It also settles which way each bay faces. A bay's forward axis is what the validator
    // measures the car's heading against, and these eight were authored before anything
    // read it: the rule applied here is that a forward or parallel bay faces the way the
    // car arrives, and a reverse bay faces back out of it.
    public static class LegacyBayPainter
    {
        private const string ScenePath = "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity";
        private const string MarkingsName = "BayMarkings";

        [MenuItem("Tools/Car Parking/Paint Original Mission Bays 1-8")]
        public static void RunInOpenScene()
        {
            int painted = Paint();
            Debug.Log($"[LegacyBayPainter] Painted {painted} bay(s). Save the scene to keep it.");
        }

        public static void RunFromCommandLine()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError($"[LegacyBayPainter] Could not open '{ScenePath}'.");
                EditorApplication.Exit(1);
                return;
            }

            int painted = Paint();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[LegacyBayPainter] Painted {painted} bay(s). Saved '{ScenePath}'.");
            EditorApplication.Exit(painted > 0 ? 0 : 1);
        }

        private static int Paint()
        {
            var manager = Object.FindFirstObjectByType<MissionManager>(FindObjectsInactive.Include);

            if (manager == null)
            {
                Debug.LogError("[LegacyBayPainter] No MissionManager in the scene.");
                return 0;
            }

            manager.RebuildRegistry();

            var kit = new MissionCourseKit();
            int painted = 0;

            foreach (MissionAuthoring mission in manager.RegisteredMissions)
            {
                if (mission.MissionId >= MissionCourseBuilder.FirstCourse)
                {
                    continue;
                }

                if (PaintMission(mission, kit))
                {
                    painted++;
                }
            }

            return painted;
        }

        private static bool PaintMission(MissionAuthoring mission, MissionCourseKit kit)
        {
            ParkingZone zone = mission.ParkingZone;
            Transform start = mission.StartPoint;
            MissionDefinition definition = mission.Definition;

            if (zone == null || start == null || definition == null)
            {
                Debug.LogWarning($"[LegacyBayPainter] Mission {mission.MissionId} has no zone, start point or definition.", mission);
                return false;
            }

            bool wasActive = mission.gameObject.activeSelf;
            mission.SetEnvironmentActive(true);

            FaceTheRightWay(zone, start, definition);

            Transform existing = mission.transform.Find(MarkingsName);

            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var markings = new GameObject(MarkingsName);
            Undo.RegisterCreatedObjectUndo(markings, "Paint bay");

            markings.transform.SetParent(mission.transform, false);
            markings.transform.SetPositionAndRotation(
                zone.WorldCenter,
                Quaternion.LookRotation(zone.ParkedHeading, Vector3.up));

            // Unit scale whatever the mission container is scaled to, so the paint is the
            // size it says it is.
            markings.transform.localScale = new Vector3(
                1f / mission.transform.lossyScale.x,
                1f / mission.transform.lossyScale.y,
                1f / mission.transform.lossyScale.z);

            // Sat on the road rather than at the zone's own height: these zones were fitted
            // to thin trigger plates that float a little above the tarmac.
            if (Physics.Raycast(zone.WorldCenter + Vector3.up * 3f, Vector3.down, out RaycastHit road, 12f))
            {
                markings.transform.position = road.point;
            }

            kit.PaintBay(markings.transform, zone.Size.x, zone.Size.z);

            mission.gameObject.SetActive(wasActive);

            Debug.Log($"[LegacyBayPainter] Mission {mission.MissionId} ('{definition.DisplayName}', {definition.ParkingType}): bay {zone.Size.x:0.0}x{zone.Size.z:0.0}m painted facing {zone.ParkedHeading:0.00}.", mission);
            return true;
        }

        // Points the bay the way a correctly parked car's nose should end up, so the
        // painted arrow and the validator agree.
        private static void FaceTheRightWay(ParkingZone zone, Transform start, MissionDefinition definition)
        {
            Vector3 approach = zone.WorldCenter - start.position;
            approach.y = 0f;

            if (approach.sqrMagnitude < 0.01f)
            {
                return;
            }

            approach.Normalize();

            bool reverse = definition.ParkingType == ParkingType.Reverse;

            // Forward and parallel: the nose carries on the way it came. Reverse: the car
            // backs in, so the nose finishes pointing back out.
            Vector3 wanted = reverse ? -approach : approach;

            zone.EditorSetParkedFacingBackward(Vector3.Dot(zone.transform.forward, wanted) < 0f);
            EditorUtility.SetDirty(zone);
        }
    }
}
