using System.Collections.Generic;
using CarParkingGame.Missions;
using CarParkingGame.Parking;
using CarParkingGame.Progression;
using UnityEditor;
using UnityEngine;

namespace CarParkingGame.EditorTools
{
    // Generates the practice-mission data: one MissionDefinition per mission, a shared
    // ScoreRules asset and the catalog the selection UI reads. Re-running it updates the
    // existing assets in place instead of creating duplicates, so the tuning table below
    // stays the single source of truth for the difficulty curve.
    //
    // This only writes assets. It never touches the gameplay scene - placing mission
    // layouts is a separate, developer-run step (see MissionSceneSetupTool).
    public static class MissionContentGenerator
    {
        private const string RootFolder = "Assets/GameAssets/ScriptableObjects";
        private const string MissionFolder = RootFolder + "/Missions";
        private const string ScoreRulesPath = RootFolder + "/DefaultScoreRules.asset";
        private const string CatalogPath = RootFolder + "/MissionCatalog.asset";

        // The master prompt fixes practice payouts at 100/200/300 coins for 1/2/3 stars,
        // so every practice mission carries the same three-star reward.
        private const int PracticeThreeStarReward = 300;

        private struct MissionSpec
        {
            public int id;
            public string name;
            public string description;
            public int difficulty;
            public ParkingType type;
            public float timeLimit;
            public float angle;
            public float hold;
            public float containment;
            public bool allowOpposite;
            public float maxSpeedKmh;
        }

        [MenuItem("Tools/Car Parking/Generate Mission Content")]
        public static void Generate()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(MissionFolder);

            ScoreRules rules = LoadOrCreate<ScoreRules>(ScoreRulesPath);

            MissionSpec[] specs = BuildSpecs();
            var definitions = new List<MissionDefinition>(specs.Length);

            foreach (MissionSpec spec in specs)
            {
                string path = $"{MissionFolder}/Mission{spec.id:00}.asset";
                MissionDefinition definition = LoadOrCreate<MissionDefinition>(path);

                definition.EditorConfigure(
                    spec.id,
                    spec.name,
                    spec.description,
                    spec.difficulty,
                    spec.type,
                    spec.timeLimit,
                    spec.timeLimit > 0f,
                    spec.maxSpeedKmh,
                    spec.angle,
                    spec.hold,
                    spec.containment,
                    spec.allowOpposite,
                    PracticeThreeStarReward,
                    rules);

                EditorUtility.SetDirty(definition);
                definitions.Add(definition);
            }

            MissionCatalog catalog = LoadOrCreate<MissionCatalog>(CatalogPath);
            catalog.EditorSetMissions(definitions);
            EditorUtility.SetDirty(catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[MissionContentGenerator] Wrote {definitions.Count} mission definitions, '{ScoreRulesPath}' and '{CatalogPath}'.");
        }

        public static void GenerateFromCommandLine()
        {
            Generate();
            EditorApplication.Exit(0);
        }

        // Difficulty curve for the thirty practice missions. Tolerances tighten, hold
        // times lengthen, containment goes from "mostly in the bay" to "fully in the
        // bay", and timed missions only start at 21 so the player learns the manoeuvre
        // before the clock is added.
        private static MissionSpec[] BuildSpecs()
        {
            return new[]
            {
                // Missions 1-8 already exist in the scene. Their parking types are taken
                // from the real ParkingTrigger.isReverseMission flags (reported by
                // MissionSceneSetupTool), not from the design list, so the existing
                // missions keep behaving the way they always have.
                Spec(1, "First Parking", "Drive forward into the bay and stop straight.", 1, ParkingType.Forward, 20f, 1.2f, 0.75f),
                Spec(2, "First Reverse", "Back into the bay instead of driving in nose first.", 2, ParkingType.Reverse, 18f, 1.3f, 0.8f),
                Spec(3, "Straight And Narrow", "Nose first again, this time between the cones.", 2, ParkingType.Forward, 16f, 1.3f, 0.8f),
                Spec(4, "Tight Reverse", "The same reverse bay with less room either side.", 2, ParkingType.Reverse, 15f, 1.4f, 0.85f),
                Spec(5, "Wide Reverse", "A ninety degree approach, then back into a generous bay.", 2, ParkingType.Reverse, 16f, 1.4f, 0.85f),
                Spec(6, "Narrow Ninety", "The same turn, into a much narrower bay.", 3, ParkingType.Forward, 14f, 1.5f, 0.9f),
                Spec(7, "Weave And Park", "Thread through the cones, then park straight.", 3, ParkingType.Forward, 14f, 1.5f, 0.9f),
                Spec(8, "Advanced Bay", "The original advanced parking challenge.", 3, ParkingType.Reverse, 13f, 1.5f, 0.9f),
                Spec(9, "Parallel Basics", "Your first parallel park, with room to spare.", 3, ParkingType.Parallel, 15f, 1.5f, 0.85f),
                Spec(10, "Between Barriers", "Parallel park between two barriers.", 3, ParkingType.Parallel, 14f, 1.5f, 0.9f),
                Spec(11, "Snug Fit", "A parallel space barely longer than the car.", 4, ParkingType.Parallel, 12f, 1.6f, 0.95f),
                Spec(12, "Reverse Parallel", "Back into the parallel space the way an examiner expects.", 4, ParkingType.Parallel, 12f, 1.6f, 0.95f),
                Spec(13, "Narrow Lane", "Follow a narrow road to the bay without scraping.", 3, ParkingType.Forward, 14f, 1.5f, 0.9f),
                Spec(14, "Lane Corners", "Tight corners in the narrow lane before you park.", 3, ParkingType.Forward, 13f, 1.5f, 0.9f),
                Spec(15, "Cone Slalom", "A full slalom run, then a straight park.", 4, ParkingType.Forward, 12f, 1.6f, 0.95f),
                Spec(16, "Slalom Reverse", "Slalom out, then reverse into the bay.", 4, ParkingType.Reverse, 12f, 1.6f, 0.95f),
                Spec(17, "Garage Entry", "Drive into a covered garage bay.", 3, ParkingType.Forward, 13f, 1.5f, 0.9f),
                Spec(18, "Garage Reverse", "Reverse into the covered garage bay.", 4, ParkingType.Reverse, 12f, 1.6f, 0.95f),
                Spec(19, "Tight Garage", "A garage bay with almost no margin.", 5, ParkingType.Reverse, 10f, 1.7f, 1f),
                Spec(20, "Construction Zone", "Park amongst barriers and site clutter.", 4, ParkingType.Forward, 12f, 1.6f, 0.95f),
                Spec(21, "Against The Clock", "Straight park, now timed.", 3, ParkingType.Forward, 14f, 1.5f, 0.9f, 45f),
                Spec(22, "Timed Reverse", "Reverse park before the clock runs out.", 4, ParkingType.Reverse, 13f, 1.5f, 0.9f, 50f),
                Spec(23, "Timed Parallel", "A parallel park under time pressure.", 4, ParkingType.Parallel, 12f, 1.6f, 0.95f, 60f),
                Spec(24, "Timed Slalom", "Slalom and reverse park against the clock.", 5, ParkingType.Reverse, 11f, 1.6f, 0.95f, 70f),
                Spec(25, "Upper Level", "Climb to the upper deck and park.", 4, ParkingType.Forward, 12f, 1.6f, 0.95f, 80f),
                Spec(26, "Upper Level Reverse", "Climb the deck and reverse into the bay.", 5, ParkingType.Reverse, 11f, 1.7f, 1f, 80f),
                Spec(27, "Multi-Storey Squeeze", "The tightest bay in the building.", 5, ParkingType.Reverse, 10f, 1.7f, 1f, 75f),
                Spec(28, "Precision", "Almost no angle tolerance. Line it up properly.", 5, ParkingType.Reverse, 6f, 2f, 1f, 0f, false, 1f),
                Spec(29, "Mixed Challenge", "Everything you have learned, in one run.", 5, ParkingType.Parallel, 10f, 1.8f, 1f, 90f),
                Spec(30, "Final Examination", "The full test. No margin, no second chances.", 5, ParkingType.Reverse, 6f, 2f, 1f, 90f, false, 1f)
            };
        }

        private static MissionSpec Spec(
            int id,
            string name,
            string description,
            int difficulty,
            ParkingType type,
            float angle,
            float hold,
            float containment,
            float timeLimit = 0f,
            bool allowOpposite = true,
            float maxSpeedKmh = 2f)
        {
            return new MissionSpec
            {
                id = id,
                name = name,
                description = description,
                difficulty = difficulty,
                type = type,
                timeLimit = timeLimit,
                angle = angle,
                hold = hold,
                containment = containment,
                allowOpposite = allowOpposite,
                maxSpeedKmh = maxSpeedKmh
            };
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            int lastSlash = folder.LastIndexOf('/');
            string parent = folder.Substring(0, lastSlash);
            string leaf = folder.Substring(lastSlash + 1);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
