using System.Collections.Generic;
using CarParkingGame.Core;
using CarParkingGame.Garage;
using CarParkingGame.Missions;
using CarParkingGame.Progression;
using CarParkingGame.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CarParkingGame.EditorTools
{
    // Checks the generated UI without entering play mode.
    //
    // Everything here is a reference the builder is supposed to have filled in, or a
    // layout rule the builder is supposed to have honoured. A missing reference on one of
    // these components shows up in play as a button that silently does nothing, which is
    // the slowest possible way to find it.
    public static class UiSelfCheck
    {
        private const string ScenePath = "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity";

        [MenuItem("Tools/Car Parking/Check Game UI")]
        public static void RunInOpenScene()
        {
            Report(Check());
        }

        public static void RunFromCommandLine()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError($"[UiSelfCheck] Could not open '{ScenePath}'.");
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
                Debug.Log("[UiSelfCheck] The game UI checks out: every reference is assigned and no two screens can show at once.");
                return;
            }

            Debug.LogError($"[UiSelfCheck] {problems.Count} problem(s):");

            foreach (string problem in problems)
            {
                Debug.LogError("  - " + problem);
            }
        }

        private static List<string> Check()
        {
            var problems = new List<string>();

            CheckReferences<GameSession>(problems, "freeRoamSpawn");
            CheckReferences<MenuController>(problems);
            CheckReferences<PauseMenuView>(problems);
            CheckReferences<GameplayHudView>(problems);
            CheckReferences<VehicleControlButtons>(problems);
            CheckReferences<BayGuideArrow>(problems);
            CheckReferences<GarageView>(problems, "thumbnail", "upgradeRows");
            CheckReferences<MissionSelectionView>(problems, "emptyStateMessage");
            CheckReferences<MissionResultView>(problems, "mainMenu");
            CheckReferences<SettingsView>(problems);
            CheckReferences<ShowroomCameraRig>(problems);

            CheckLegacyUiIsOff(problems);
            CheckScreensAreExclusive(problems);
            CheckGarageIsLive(problems);
            CheckPracticeGate(problems);

            return problems;
        }

        // Walks the component's serialized object instead of naming fields one by one, so
        // a field added later is covered without editing this file.
        private static void CheckReferences<T>(List<string> problems, params string[] optionalFields)
            where T : Component
        {
            T[] components = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (components.Length == 0)
            {
                problems.Add($"No {typeof(T).Name} in the scene.");
                return;
            }

            var optional = new HashSet<string>(optionalFields);

            foreach (T component in components)
            {
                var serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();

                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference)
                    {
                        continue;
                    }

                    if (property.objectReferenceValue != null || optional.Contains(property.name))
                    {
                        continue;
                    }

                    problems.Add($"{typeof(T).Name} on '{component.name}': '{property.name}' is empty.");
                }

                CheckArrays(problems, component, serialized, optional);
            }
        }

        private static void CheckArrays(List<string> problems, Component component, SerializedObject serialized, HashSet<string> optional)
        {
            SerializedProperty property = serialized.GetIterator();

            while (property.NextVisible(true))
            {
                if (!property.isArray || property.propertyType == SerializedPropertyType.String || optional.Contains(property.name))
                {
                    continue;
                }

                if (property.arraySize == 0)
                {
                    problems.Add($"{component.GetType().Name} on '{component.name}': '{property.name}' is an empty list.");
                    continue;
                }

                for (int i = 0; i < property.arraySize; i++)
                {
                    SerializedProperty element = property.GetArrayElementAtIndex(i);

                    if (element.propertyType == SerializedPropertyType.ObjectReference && element.objectReferenceValue == null)
                    {
                        problems.Add($"{component.GetType().Name} on '{component.name}': '{property.name}[{i}]' is empty.");
                    }
                }
            }
        }

        // The overlapping screens in the bug report were two live canvases, not a layout
        // mistake, so this is checked rather than eyeballed.
        // What a new player can actually tap in Practice, worked out the way the mission
        // card works it out rather than the way the screenshot tool pretends.
        //
        // MissionUnlocking.EveryMissionOpen is a switch, so this checks the behaviour the
        // switch is currently set to: every course open while the courses are being
        // reviewed, or mission one alone on a fresh save once it goes back.
        private static void CheckPracticeGate(List<string> problems)
        {
            var manager = Object.FindFirstObjectByType<MissionManager>(FindObjectsInactive.Include);

            if (manager == null)
            {
                problems.Add("No MissionManager in the scene, so nothing can be selected in Practice.");
                return;
            }

            manager.RebuildRegistry();

            int open = 0;
            var shut = new List<int>();

            foreach (MissionAuthoring mission in manager.RegisteredMissions)
            {
                // A fresh save: nothing completed, so no progress entry for any of them.
                if (MissionUnlocking.IsOpen(mission.MissionId, null))
                {
                    open++;
                }
                else
                {
                    shut.Add(mission.MissionId);
                }
            }

            int total = manager.RegisteredMissions.Count;

            if (MissionUnlocking.EveryMissionOpen)
            {
                if (open != total)
                {
                    problems.Add($"Practice is meant to be wide open, but {shut.Count} of {total} missions are still locked ({string.Join(", ", shut)}).");
                    return;
                }

                Debug.Log($"[UiSelfCheck] Practice is open: all {total} missions can be picked without playing through.");
                return;
            }

            if (open != 1)
            {
                problems.Add($"Sequential unlocking is back on, but {open} missions are open on a fresh save instead of just the first.");
                return;
            }

            Debug.Log($"[UiSelfCheck] Practice unlocks in order: 1 of {total} missions open on a fresh save.");
        }

        private static void CheckLegacyUiIsOff(List<string> problems)
        {
            foreach (string name in new[] { "MainMenuCanvas", "MissionInfo", "MobileControls" })
            {
                GameObject legacy = FindInScene(name);

                if (legacy != null && legacy.activeSelf)
                {
                    problems.Add($"The legacy canvas '{name}' is still switched on; it will draw over the new UI.");
                }
            }
        }

        private static void CheckScreensAreExclusive(List<string> problems)
        {
            var menu = Object.FindFirstObjectByType<MenuController>(FindObjectsInactive.Include);

            if (menu == null)
            {
                return;
            }

            var serialized = new SerializedObject(menu);
            var active = new List<string>();

            foreach (string field in new[] { "homeScreen", "modeScreen", "practiceScreen", "garageScreen", "settingsScreen" })
            {
                SerializedProperty property = serialized.FindProperty(field);
                var screen = property?.objectReferenceValue as GameObject;

                if (screen != null && screen.activeSelf)
                {
                    active.Add(screen.name);
                }
            }

            if (active.Count > 1)
            {
                problems.Add($"{active.Count} menu screens are switched on at once ({string.Join(", ", active)}).");
            }
        }

        private static void CheckGarageIsLive(List<string> problems)
        {
            var garage = Object.FindFirstObjectByType<GarageManager>(FindObjectsInactive.Include);

            if (garage == null)
            {
                problems.Add("No GarageManager in the scene.");
                return;
            }

            if (!garage.enabled)
            {
                problems.Add("GarageManager is disabled, so the garage will show no car.");
            }

            if (garage.ShowroomCarContainer == null)
            {
                problems.Add("GarageManager has no showroom car container, so the garage will show no car.");
            }
        }

        private static GameObject FindInScene(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            return null;
        }
    }
}
