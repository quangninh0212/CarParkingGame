using CarParkingGame.Missions;
using CarParkingGame.Parking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarParkingGame.EditorTools
{
    // Places each mission's bay from what the legacy layout actually encodes.
    //
    // Measured across all eight original missions (see MissionGeometryProbe):
    //  - every legacy trigger is a thin plate (most scaled 1 x 2.3 x 0.19), i.e. the END
    //    LINE of the bay, not the bay itself;
    //  - the plates were rotated by hand, and where the surrounding cones form a clear
    //    line their axis agrees with the plate's forward axis to within 0-8 degrees;
    //  - in 8 of 8 missions the cones sit on the plate's +forward side, 2-4 m out,
    //    i.e. the bay extends from the plate along +forward.
    //
    // The legacy completion rule then fixes the parked heading: forward missions fired
    // when the car's body touched the plate (nose at the end line), reverse missions
    // when its rear-tagged collider did (tail at the end line).
    public static class BayFitTool
    {
        private const string ScenePath = "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity";

        // The widest car is ~1.94 m and its measured footprint ~3.7 m long, so the
        // hardest bay still leaves ~18 cm a side. Easy bays are a generous real-world size.
        private const float EasyWidth = 2.8f;
        private const float HardWidth = 2.3f;
        private const float EasyLength = 6.0f;
        private const float HardLength = 5.0f;

        // The legacy rule completed as soon as the bumper touched the end line, so a
        // properly parked car's bumper sits slightly past it.
        private const float BehindEndLine = 0.6f;

        private const float BayHeight = 2.5f;

        // Each mission shows the player a glowing target (Rays / Runes / Point Light). On
        // mission 8 it measured 3.6 m out from the end line, while the end-line rule put
        // the bay centre at 2.15 m - a car parked dead centre on the target would have
        // hung half a metre out of the bay and failed. The target is what the player
        // aims at, so when it exists the bay is centred on it.
        private const float MarkerSearchRadius = 10f;

        private static readonly string[] MarkerNames = { "Runes", "Rays", "Runes small", "Point Light" };

        public static string Fit(ParkingZone zone, MissionDefinition definition, GameObject environment)
        {
            float tightness = Mathf.InverseLerp(1f, 5f, definition.Difficulty);
            float width = Mathf.Lerp(EasyWidth, HardWidth, tightness);
            float length = Mathf.Lerp(EasyLength, HardLength, tightness);

            Vector3 centre;
            string placement;

            if (TryFindMarker(zone.transform, environment, out Vector3 marker))
            {
                centre = new Vector3(marker.x, 0f, marker.z);
                placement = $"centred on the glowing target ({marker.x:0.0}, {marker.z:0.0}) m from the end line";
            }
            else
            {
                centre = new Vector3(0f, 0f, length * 0.5f - BehindEndLine);
                placement = "no target found, extended from the end line";
            }

            zone.EditorSetBox(centre, new Vector3(width, BayHeight, length));
            zone.EditorSetParkedFacingBackward(definition.ParkingType == ParkingType.Forward);
            EditorUtility.SetDirty(zone);

            return placement;
        }

        // Returns the target's position in the zone's rotation frame (metres, scale ignored,
        // +z along the end line's forward axis).
        private static bool TryFindMarker(Transform zone, GameObject environment, out Vector3 local)
        {
            local = Vector3.zero;

            if (environment == null)
            {
                return false;
            }

            Transform[] nodes = environment.GetComponentsInChildren<Transform>(true);

            foreach (string markerName in MarkerNames)
            {
                foreach (Transform node in nodes)
                {
                    if (node.name != markerName)
                    {
                        continue;
                    }

                    Vector3 offset = node.position - zone.position;
                    offset.y = 0f;

                    if (offset.magnitude > MarkerSearchRadius)
                    {
                        continue;
                    }

                    local = Quaternion.Inverse(zone.rotation) * offset;
                    return true;
                }
            }

            return false;
        }

        [MenuItem("Tools/Car Parking/Fit Parking Bays To Legacy Layout")]
        public static void FitAllInOpenScene()
        {
            FitAll();
        }

        public static void FitAllFromCommandLine()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                EditorApplication.Exit(1);
                return;
            }

            FitAll();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[BayFitTool] Saved '{ScenePath}'.");
            EditorApplication.Exit(0);
        }

        private static void FitAll()
        {
            MissionAuthoring[] all = Object.FindObjectsByType<MissionAuthoring>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int fitted = 0;

            foreach (MissionAuthoring mission in all)
            {
                if (mission.Definition == null || mission.ParkingZone == null)
                {
                    continue;
                }

                string placement = Fit(mission.ParkingZone, mission.Definition, mission.EnvironmentContainer);
                fitted++;

                Vector3 size = mission.ParkingZone.Size;
                Debug.Log($"[BayFitTool] Mission {mission.MissionId} ({mission.Definition.ParkingType}, difficulty {mission.Definition.Difficulty}): bay {size.x:0.00} x {size.z:0.00} m, {placement}.");
            }

            Debug.Log($"[BayFitTool] Fitted {fitted} bay(s).");
        }
    }
}
