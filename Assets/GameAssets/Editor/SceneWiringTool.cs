using System.Collections.Generic;
using CarParkingGame.Garage;
using CarParkingGame.Missions;
using CarParkingGame.Progression;
using CarParkingGame.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarParkingGame.EditorTools
{
    // Puts the new managers into the gameplay scene and converts the eight legacy missions.
    //
    // Scope is deliberately limited to things that can be derived from the scene itself:
    //  - mission authoring, read from the legacy GameManager's arrays,
    //  - manager objects with their asset references assigned,
    //  - the car containers, found from the actual CarController/CarSelection wiring
    //    rather than by guessing object names.
    //
    // It does NOT build UI, assign body renderers for paint, or place lights. Those need
    // judgement about what the scene looks like, and a blindly generated canvas would only
    // have to be redone by hand.
    public static class SceneWiringTool
    {
        private const string ScenePath = "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity";
        private const string ScoreRulesPath = "Assets/GameAssets/ScriptableObjects/DefaultScoreRules.asset";
        private const string QualityProfilePath = "Assets/GameAssets/ScriptableObjects/QualityProfile.asset";
        private const string CarCatalogPath = "Assets/GameAssets/ScriptableObjects/CarCatalog.asset";
        private const string ColorPalettePath = "Assets/GameAssets/ScriptableObjects/CarColorPalette.asset";
        private const string UpgradeRulesPath = "Assets/GameAssets/ScriptableObjects/UpgradeRules.asset";

        [MenuItem("Tools/Car Parking/Wire Gameplay Scene")]
        public static void WireOpenScene()
        {
            if (Wire())
            {
                Debug.Log("[SceneWiringTool] Done. Save the scene to keep the changes.");
            }
        }

        public static void WireFromCommandLine()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError($"[SceneWiringTool] Could not open '{ScenePath}'.");
                EditorApplication.Exit(1);
                return;
            }

            bool wired = Wire();

            if (wired)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[SceneWiringTool] Saved '{ScenePath}'.");
            }

            EditorApplication.Exit(wired ? 0 : 1);
        }

        // Opens the gameplay scene and runs mission validation, so the check can be run
        // headlessly instead of only from the menu with the scene already open.
        public static void ValidateFromCommandLine()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError($"[SceneWiringTool] Could not open '{ScenePath}'.");
                EditorApplication.Exit(1);
                return;
            }

            MissionSceneSetupTool.ValidateMissions();
            EditorApplication.Exit(0);
        }

        private static bool Wire()
        {
            var gameManager = Object.FindFirstObjectByType<GameManager>();

            if (gameManager == null)
            {
                Debug.LogError("[SceneWiringTool] No GameManager in the scene; this does not look like the gameplay scene.");
                return false;
            }

            MissionSceneSetupTool.SetUpFromLegacyGameManager();

            GameObject playerContainer = ResolvePlayerCarContainer();
            GameObject showroomContainer = ResolveShowroomContainer(playerContainer);

            WireMissionManager();
            WireSettingsManager();
            WireGarageManager(playerContainer, showroomContainer);

            return true;
        }

        private static void WireMissionManager()
        {
            MissionManager manager = FindOrCreate<MissionManager>("MissionManager");
            var serialized = new SerializedObject(manager);

            AssignAsset<ScoreRules>(serialized, "defaultScoreRules", ScoreRulesPath);
            serialized.ApplyModifiedProperties();

            Debug.Log("[SceneWiringTool] MissionManager is in the scene and inert: nothing calls StartMission yet, so the legacy GameManager still runs missions. Wire MissionSelectionView to hand over.");
        }

        private static void WireSettingsManager()
        {
            SettingsManager manager = FindOrCreate<SettingsManager>("SettingsManager");
            var serialized = new SerializedObject(manager);

            AssignAsset<QualityProfile>(serialized, "qualityProfile", QualityProfilePath);
            serialized.ApplyModifiedProperties();

            Debug.Log("[SettingsManager] will now apply the saved graphics tier and frame rate at startup, and will create the JSON save file on first run (migrating the legacy PlayerPrefs, which are left intact).");
        }

        private static void WireGarageManager(GameObject playerContainer, GameObject showroomContainer)
        {
            GarageManager manager = FindOrCreate<GarageManager>("GarageManager");
            var serialized = new SerializedObject(manager);

            AssignAsset<CarCatalog>(serialized, "catalog", CarCatalogPath);
            AssignAsset<CarColorPalette>(serialized, "palette", ColorPalettePath);
            AssignAsset<UpgradeRules>(serialized, "upgradeRules", UpgradeRulesPath);

            AssignObject(serialized, "playerCarContainer", playerContainer);
            AssignObject(serialized, "showroomCarContainer", showroomContainer);

            serialized.ApplyModifiedProperties();

            // Left disabled on purpose: enabling it would have it activate and paint cars
            // alongside the legacy CarSelection, and nothing drives it until GarageView
            // exists. The tedious part - the references - is done.
            manager.enabled = false;
            EditorUtility.SetDirty(manager);

            Debug.Log("[SceneWiringTool] GarageManager is wired but DISABLED. Enable it once GarageView is built, and remove the legacy CarSelection components at the same time.");
        }

        // The drivable cars are whatever the CarControllers hang off, which is more
        // reliable than matching an object called "PlayerCars".
        private static GameObject ResolvePlayerCarContainer()
        {
            CarController[] cars = Object.FindObjectsByType<CarController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            if (cars.Length == 0)
            {
                Debug.LogWarning("[SceneWiringTool] No CarController in the scene; the player-car container could not be found.");
                return null;
            }

            Transform parent = cars[0].transform.parent;

            foreach (CarController car in cars)
            {
                if (car.transform.parent != parent)
                {
                    Debug.LogWarning($"[SceneWiringTool] The cars do not share one parent ('{car.name}' differs), so the container had to be guessed as '{parent?.name}'. Check GarageManager's player-car container by hand.");
                    break;
                }
            }

            if (parent != null)
            {
                Debug.Log($"[SceneWiringTool] Player-car container resolved to '{parent.name}' with {parent.childCount} children.");
            }

            return parent != null ? parent.gameObject : null;
        }

        // The showroom is the other CarSelection's container - the scene has two, one for
        // the menu cars and one for the drivable cars.
        private static GameObject ResolveShowroomContainer(GameObject playerContainer)
        {
            CarSelection[] selections = Object.FindObjectsByType<CarSelection>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            var candidates = new List<GameObject>();

            foreach (CarSelection selection in selections)
            {
                GameObject container = selection.allCarsContainer;

                if (container != null && container != playerContainer && !candidates.Contains(container))
                {
                    candidates.Add(container);
                }
            }

            if (candidates.Count == 0)
            {
                Debug.LogWarning("[SceneWiringTool] Could not find a showroom container distinct from the player-car container; assign GarageManager's showroom container by hand.");
                return null;
            }

            if (candidates.Count > 1)
            {
                Debug.LogWarning($"[SceneWiringTool] Found {candidates.Count} possible showroom containers; used '{candidates[0].name}'. Check it by hand.");
            }

            Debug.Log($"[SceneWiringTool] Showroom container resolved to '{candidates[0].name}'.");
            return candidates[0];
        }

        private static T FindOrCreate<T>(string objectName) where T : MonoBehaviour
        {
            T existing = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

            if (existing != null)
            {
                Debug.Log($"[SceneWiringTool] Reusing the existing {typeof(T).Name} on '{existing.name}'.");
                return existing;
            }

            var host = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(host, $"Create {objectName}");

            T component = host.AddComponent<T>();
            Debug.Log($"[SceneWiringTool] Created '{objectName}' with a {typeof(T).Name}.");
            return component;
        }

        private static void AssignAsset<T>(SerializedObject serialized, string fieldName, string assetPath)
            where T : ScriptableObject
        {
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogWarning($"[SceneWiringTool] No serialized field '{fieldName}' on {serialized.targetObject.GetType().Name}.");
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);

            if (asset == null)
            {
                Debug.LogWarning($"[SceneWiringTool] No asset at '{assetPath}'; '{fieldName}' left empty.");
                return;
            }

            property.objectReferenceValue = asset;
        }

        private static void AssignObject(SerializedObject serialized, string fieldName, GameObject value)
        {
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogWarning($"[SceneWiringTool] No serialized field '{fieldName}' on {serialized.targetObject.GetType().Name}.");
                return;
            }

            if (value == null)
            {
                Debug.LogWarning($"[SceneWiringTool] '{fieldName}' could not be resolved and was left empty.");
                return;
            }

            property.objectReferenceValue = value;
        }
    }
}
