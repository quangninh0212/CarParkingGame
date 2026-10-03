using System.Collections.Generic;
using CarParkingGame.Missions;
using CarParkingGame.Parking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarParkingGame.EditorTools
{
    // Builds layouts for missions 9-30 by cloning the eight that already work.
    //
    // The trick that makes this safe without knowing the track's layout: mission
    // environments are activated one at a time (only the current mission's container is
    // enabled), so clones can sit at exactly the same world position as their source and
    // never coexist. Nothing has to be found a free patch of ground.
    //
    // Difficulty comes from geometry derived from each bay's own transform - the bay is
    // narrowed and cones are placed along its approach using the bay's local axes - rather
    // than from invented coordinates. A bay is never allowed to shrink below the car that
    // has to fit in it.
    public static class MissionLayoutGenerator
    {
        private const string ScenePath = "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity";
        private const string CatalogPath = "Assets/GameAssets/ScriptableObjects/MissionCatalog.asset";

        private const int FirstGeneratedMission = 9;
        private const int LastGeneratedMission = 30;

        [MenuItem("Tools/Car Parking/Generate Mission Layouts 9-30")]
        public static void GenerateInOpenScene()
        {
            Generate();
        }

        public static void GenerateFromCommandLine()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError($"[MissionLayoutGenerator] Could not open '{ScenePath}'.");
                EditorApplication.Exit(1);
                return;
            }

            bool ok = Generate();

            if (ok)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[MissionLayoutGenerator] Saved '{ScenePath}'.");
            }

            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Generate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MissionCatalog>(CatalogPath);

            if (catalog == null)
            {
                Debug.LogError($"[MissionLayoutGenerator] No catalog at '{CatalogPath}'.");
                return false;
            }

            List<MissionAuthoring> sources = CollectSourceMissions();

            if (sources.Count == 0)
            {
                Debug.LogError("[MissionLayoutGenerator] No existing MissionAuthoring to clone. Run the mission setup tool first.");
                return false;
            }

            Debug.Log($"[MissionLayoutGenerator] Cloning from {sources.Count} working mission(s).");

            int created = 0;
            int skipped = 0;

            for (int missionId = FirstGeneratedMission; missionId <= LastGeneratedMission; missionId++)
            {
                MissionDefinition definition = catalog.Find(missionId);

                if (definition == null)
                {
                    Debug.LogWarning($"[MissionLayoutGenerator] No definition for mission {missionId}; skipped.");
                    skipped++;
                    continue;
                }

                if (FindExisting(missionId) != null)
                {
                    skipped++;
                    continue;
                }

                // Clone a source whose parking type matches, so a reverse mission starts
                // from a layout that was authored to be reversed into.
                MissionAuthoring source = PickSource(sources, definition, missionId);

                if (!CloneMission(source, definition, missionId))
                {
                    skipped++;
                    continue;
                }

                created++;
            }

            Debug.Log($"[MissionLayoutGenerator] Created {created} mission layout(s), skipped {skipped}.");
            return true;
        }

        private static List<MissionAuthoring> CollectSourceMissions()
        {
            var sources = new List<MissionAuthoring>();

            MissionAuthoring[] all = Object.FindObjectsByType<MissionAuthoring>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (MissionAuthoring authoring in all)
            {
                bool usable = authoring.Definition != null
                              && authoring.Definition.MissionId < FirstGeneratedMission
                              && authoring.ParkingZone != null
                              && authoring.StartPoint != null
                              && authoring.EnvironmentContainer != null;

                if (usable)
                {
                    sources.Add(authoring);
                }
            }

            sources.Sort((a, b) => a.Definition.MissionId.CompareTo(b.Definition.MissionId));
            return sources;
        }

        private static MissionAuthoring FindExisting(int missionId)
        {
            MissionAuthoring[] all = Object.FindObjectsByType<MissionAuthoring>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (MissionAuthoring authoring in all)
            {
                if (authoring.MissionId == missionId)
                {
                    return authoring;
                }
            }

            return null;
        }

        private static MissionAuthoring PickSource(List<MissionAuthoring> sources, MissionDefinition definition, int missionId)
        {
            var matching = new List<MissionAuthoring>();

            foreach (MissionAuthoring source in sources)
            {
                if (source.Definition.ParkingType == definition.ParkingType)
                {
                    matching.Add(source);
                }
            }

            List<MissionAuthoring> pool = matching.Count > 0 ? matching : sources;
            return pool[missionId % pool.Count];
        }

        private static bool CloneMission(MissionAuthoring source, MissionDefinition definition, int missionId)
        {
            GameObject sourceContainer = source.EnvironmentContainer;

            // Instantiate as a sibling at the same transform. Unity remaps references that
            // point inside the duplicated hierarchy, so the clone's authoring ends up
            // pointing at the clone's own bay and start point.
            GameObject clone = Object.Instantiate(
                sourceContainer,
                sourceContainer.transform.position,
                sourceContainer.transform.rotation,
                sourceContainer.transform.parent);

            clone.name = $"Mission{missionId:00}_Generated";
            Undo.RegisterCreatedObjectUndo(clone, "Generate mission layout");

            MissionAuthoring authoring = clone.GetComponent<MissionAuthoring>();

            if (authoring == null)
            {
                authoring = clone.AddComponent<MissionAuthoring>();
            }

            ParkingZone zone = FindZone(clone, source);

            if (zone == null)
            {
                Debug.LogWarning($"[MissionLayoutGenerator] Mission {missionId}: the clone has no ParkingZone; removed.", clone);
                Object.DestroyImmediate(clone);
                return false;
            }

            Transform startPoint = ResolveStartPoint(clone, source);

            authoring.EditorAssign(definition, startPoint, zone, clone);
            EditorUtility.SetDirty(authoring);

            // The legacy trigger would complete the mission on contact, bypassing the new
            // validation. Generated missions only ever run through MissionManager.
            foreach (ParkingTrigger legacy in clone.GetComponentsInChildren<ParkingTrigger>(true))
            {
                Object.DestroyImmediate(legacy);
            }

            BayFitTool.Fit(zone, definition, clone);

            clone.SetActive(false);

            Debug.Log($"[MissionLayoutGenerator] Mission {missionId} ('{definition.DisplayName}', {definition.ParkingType}) cloned from mission {source.Definition.MissionId}.", clone);
            return true;
        }

        private static ParkingZone FindZone(GameObject clone, MissionAuthoring source)
        {
            MissionAuthoring cloneAuthoring = clone.GetComponent<MissionAuthoring>();

            if (cloneAuthoring != null && cloneAuthoring.ParkingZone != null
                && cloneAuthoring.ParkingZone.transform.IsChildOf(clone.transform))
            {
                return cloneAuthoring.ParkingZone;
            }

            return clone.GetComponentInChildren<ParkingZone>(true);
        }

        // If the start point lived inside the container it was cloned too; if it sat
        // outside, every clone shares the original, which is the same spot anyway.
        private static Transform ResolveStartPoint(GameObject clone, MissionAuthoring source)
        {
            MissionAuthoring cloneAuthoring = clone.GetComponent<MissionAuthoring>();

            if (cloneAuthoring != null && cloneAuthoring.StartPoint != null)
            {
                return cloneAuthoring.StartPoint;
            }

            return source.StartPoint;
        }

    }
}
