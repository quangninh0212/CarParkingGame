using System.Collections.Generic;
using CarParkingGame.Missions;
using CarParkingGame.Parking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CarParkingGame.EditorTools
{
    // Converts the existing eight missions into the new data-driven setup by reading the
    // legacy GameManager's arrays, rather than asking anyone to re-author them by hand.
    //
    // Deliberately a manual, undoable action: it edits the gameplay scene, and the result
    // has to be looked at in the Editor. Run it with complete_track_demo.unity open.
    public static class MissionSceneSetupTool
    {
        private const string CatalogPath = "Assets/GameAssets/ScriptableObjects/MissionCatalog.asset";

        [MenuItem("Tools/Car Parking/Set Up Mission Authoring From Legacy GameManager")]
        public static void SetUpFromLegacyGameManager()
        {
            var gameManager = Object.FindFirstObjectByType<GameManager>();

            if (gameManager == null)
            {
                Debug.LogError("[MissionSceneSetupTool] No GameManager in the open scene. Open complete_track_demo.unity first.");
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<MissionCatalog>(CatalogPath);

            if (catalog == null)
            {
                Debug.LogError($"[MissionSceneSetupTool] No mission catalog at '{CatalogPath}'. Run Tools > Car Parking > Generate Mission Content first.");
                return;
            }

            int count = Mathf.Min(
                gameManager.missionStartPoints != null ? gameManager.missionStartPoints.Length : 0,
                gameManager.missionAreas != null ? gameManager.missionAreas.Length : 0,
                gameManager.parkingTriggers != null ? gameManager.parkingTriggers.Length : 0);

            if (count == 0)
            {
                Debug.LogError("[MissionSceneSetupTool] The GameManager has no mission arrays wired up; nothing to convert.");
                return;
            }

            var warnings = new List<string>();
            int converted = 0;

            Undo.SetCurrentGroupName("Set up mission authoring");
            int undoGroup = Undo.GetCurrentGroup();

            for (int i = 0; i < count; i++)
            {
                int missionId = i + 1;
                MissionDefinition definition = catalog.Find(missionId);

                if (definition == null)
                {
                    warnings.Add($"No definition for mission {missionId} in the catalog; skipped.");
                    continue;
                }

                Transform startPoint = gameManager.missionStartPoints[i];
                GameObject area = gameManager.missionAreas[i];
                ParkingTrigger trigger = gameManager.parkingTriggers[i];

                if (startPoint == null || area == null || trigger == null)
                {
                    warnings.Add($"Mission {missionId} is missing a start point, area or parking trigger in the GameManager arrays; skipped.");
                    continue;
                }

                ParkingZone zone = EnsureParkingZone(trigger, warnings, missionId);
                MissionAuthoring authoring = EnsureAuthoring(area);

                Undo.RecordObject(authoring, "Set up mission authoring");
                authoring.EditorAssign(definition, startPoint, zone, area);
                EditorUtility.SetDirty(authoring);

                bool sceneSaysReverse = trigger.isReverseMission;
                bool definitionSaysReverse = definition.ParkingType == ParkingType.Reverse;

                if (sceneSaysReverse != definitionSaysReverse)
                {
                    warnings.Add(
                        $"Mission {missionId}: the scene's trigger says isReverseMission={sceneSaysReverse} but the definition is {definition.ParkingType}. " +
                        "The scene is the existing behaviour - change the definition if the scene is right.");
                }

                converted++;
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkAllScenesDirty();

            Debug.Log($"[MissionSceneSetupTool] Converted {converted} of {count} legacy missions. Save the scene to keep the changes (Ctrl+Z undoes it).");

            foreach (string warning in warnings)
            {
                Debug.LogWarning("[MissionSceneSetupTool] " + warning);
            }
        }

        [MenuItem("Tools/Car Parking/Validate Missions")]
        public static void ValidateMissions()
        {
            var authorings = Object.FindObjectsByType<MissionAuthoring>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            var issues = new List<string>();
            var seenIds = new Dictionary<int, string>();

            foreach (MissionAuthoring authoring in authorings)
            {
                authoring.CollectIssues(issues);

                int id = authoring.MissionId;

                if (id <= 0)
                {
                    continue;
                }

                if (seenIds.TryGetValue(id, out string owner))
                {
                    issues.Add($"Duplicate mission id {id} on '{authoring.name}' (already used by '{owner}').");
                }
                else
                {
                    seenIds.Add(id, authoring.name);
                }
            }

            var catalog = AssetDatabase.LoadAssetAtPath<MissionCatalog>(CatalogPath);

            if (catalog == null)
            {
                issues.Add($"No mission catalog at '{CatalogPath}'.");
            }
            else
            {
                foreach (MissionDefinition definition in catalog.Missions)
                {
                    if (definition != null && !seenIds.ContainsKey(definition.MissionId))
                    {
                        issues.Add($"Mission {definition.MissionId} ('{definition.DisplayName}') has no layout in this scene yet; it will show as locked.");
                    }
                }
            }

            if (issues.Count == 0)
            {
                Debug.Log($"[MissionSceneSetupTool] Validated {authorings.Length} missions with no issues.");
                return;
            }

            Debug.LogWarning($"[MissionSceneSetupTool] Validated {authorings.Length} missions and found {issues.Count} issue(s):");

            foreach (string issue in issues)
            {
                Debug.LogWarning("  - " + issue);
            }
        }

        private static ParkingZone EnsureParkingZone(ParkingTrigger trigger, List<string> warnings, int missionId)
        {
            ParkingZone zone = trigger.GetComponent<ParkingZone>();

            if (zone == null)
            {
                zone = Undo.AddComponent<ParkingZone>(trigger.gameObject);
            }

            var box = trigger.GetComponent<BoxCollider>();

            if (box != null)
            {
                Undo.RecordObject(zone, "Set up parking zone");
                zone.EditorSetBox(box.center, box.size);
                EditorUtility.SetDirty(zone);
            }
            else
            {
                warnings.Add($"Mission {missionId}: the parking trigger has no BoxCollider, so the bay kept its default size. Set it by hand on '{trigger.name}'.");
            }

            return zone;
        }

        private static MissionAuthoring EnsureAuthoring(GameObject area)
        {
            MissionAuthoring authoring = area.GetComponent<MissionAuthoring>();

            return authoring != null ? authoring : Undo.AddComponent<MissionAuthoring>(area);
        }
    }
}
