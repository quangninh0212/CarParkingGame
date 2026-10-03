using System.Collections.Generic;
using CarParkingGame.Missions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarParkingGame.EditorTools
{
    // Read-only measurement of each legacy mission's layout, to decide where the real
    // parking bay is and which way it faces before resizing anything.
    public static class MissionGeometryProbe
    {
        private const string ScenePath = "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity";
        private const float NeighbourRadius = 8f;

        public static void ProbeFromCommandLine()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                EditorApplication.Exit(1);
                return;
            }

            Probe();
            EditorApplication.Exit(0);
        }

        [MenuItem("Tools/Car Parking/Probe Mission Geometry")]
        public static void Probe()
        {
            MissionAuthoring[] all = Object.FindObjectsByType<MissionAuthoring>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var originals = new List<MissionAuthoring>();

            foreach (MissionAuthoring authoring in all)
            {
                if (authoring.Definition != null && authoring.Definition.MissionId <= 8)
                {
                    originals.Add(authoring);
                }
            }

            originals.Sort((a, b) => a.MissionId.CompareTo(b.MissionId));

            foreach (MissionAuthoring mission in originals)
            {
                Transform trigger = mission.ParkingZone.transform;
                Transform start = mission.StartPoint;

                Vector3 approach = trigger.position - start.position;
                approach.y = 0f;

                float approachYaw = approach.sqrMagnitude > 0.01f ? Quaternion.LookRotation(approach).eulerAngles.y : float.NaN;

                var collider = trigger.GetComponent<BoxCollider>();
                Vector3 worldSize = collider != null ? Vector3.Scale(collider.size, trigger.lossyScale) : Vector3.zero;

                Debug.Log(
                    $"[Probe] M{mission.MissionId} {mission.Definition.ParkingType,-8} " +
                    $"triggerYaw={trigger.eulerAngles.y:0} triggerScale={trigger.lossyScale} boxWorld={worldSize} " +
                    $"startYaw={start.eulerAngles.y:0} approachYaw={approachYaw:0} dist={approach.magnitude:0.0} " +
                    $"startInsideContainer={start.IsChildOf(mission.EnvironmentContainer.transform)}");

                LogNeighbours(mission, trigger.position);
                LogMarkers(mission, trigger);
            }
        }

        // Each mission carries a glowing marker (Rays / Runes / Point Light). If that is
        // where the player is shown to park, the bay has to sit on it, so report where it
        // is in the trigger's own frame: +z is the trigger's forward axis.
        private static void LogMarkers(MissionAuthoring mission, Transform trigger)
        {
            Quaternion inverse = Quaternion.Inverse(trigger.rotation);
            int found = 0;

            foreach (Transform node in mission.EnvironmentContainer.GetComponentsInChildren<Transform>(true))
            {
                string n = node.name;

                if (n != "Runes" && n != "Runes small" && n != "Rays" && n != "Point Light")
                {
                    continue;
                }

                Vector3 local = inverse * (node.position - trigger.position);
                Debug.Log($"[Probe]    marker '{n}' trigger-frame x={local.x:0.0} z={local.z:0.0} (y={local.y:0.0}) scale={node.lossyScale}");
                found++;
            }

            if (found == 0)
            {
                Debug.Log("[Probe]    no Rays/Runes/Point Light marker inside this mission's container");
            }
        }

        // The cones and barriers around a bay outline it, so their spread tells us the
        // bay's long axis independently of the trigger's own rotation.
        private static void LogNeighbours(MissionAuthoring mission, Vector3 centre)
        {
            var points = new List<Vector3>();

            foreach (Collider collider in mission.EnvironmentContainer.GetComponentsInChildren<Collider>(true))
            {
                if (collider.isTrigger)
                {
                    continue;
                }

                Vector3 p = collider.bounds.center;

                if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(centre.x, centre.z)) <= NeighbourRadius)
                {
                    points.Add(p);
                }
            }

            if (points.Count < 3)
            {
                Debug.Log($"[Probe]    neighbours within {NeighbourRadius}m: {points.Count} (too few for an axis)");
                return;
            }

            Vector3 mean = Vector3.zero;

            foreach (Vector3 p in points)
            {
                mean += p;
            }

            mean /= points.Count;

            float sxx = 0f, szz = 0f, sxz = 0f;

            foreach (Vector3 p in points)
            {
                float dx = p.x - mean.x;
                float dz = p.z - mean.z;
                sxx += dx * dx;
                szz += dz * dz;
                sxz += dx * dz;
            }

            // Principal axis of a 2x2 covariance matrix.
            float angle = 0.5f * Mathf.Atan2(2f * sxz, sxx - szz);
            float axisYaw = (90f - angle * Mathf.Rad2Deg + 360f) % 180f;

            float trace = sxx + szz;
            float det = sxx * szz - sxz * sxz;
            float gap = Mathf.Sqrt(Mathf.Max(0f, trace * trace / 4f - det));
            float l1 = trace / 2f + gap;
            float l2 = trace / 2f - gap;
            float elongation = l2 > 0.0001f ? l1 / l2 : 999f;

            Vector3 offset = mean - centre;

            Debug.Log(
                $"[Probe]    neighbours={points.Count} axisYaw={axisYaw:0} (mod 180) elongation={elongation:0.0} " +
                $"clusterOffsetFromTrigger=({offset.x:0.0},{offset.z:0.0})");
        }
    }
}
