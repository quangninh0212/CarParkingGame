using System.Collections.Generic;
using CarParkingGame.Garage;
using CarParkingGame.Settings;
using UnityEditor;
using UnityEngine;

namespace CarParkingGame.EditorTools
{
    // Generates the garage, upgrade and quality assets. Writes assets only - it never
    // touches the scene.
    //
    // The car entries keep the project's existing three cars and the prices from the
    // design brief (first car free, then 3,000 and 6,000 coins). Each definition's
    // carIndex is its child position in the player-car container, which is how the
    // existing CarSelection has always identified cars; run
    // Tools > Car Parking > Validate Garage with the gameplay scene open to confirm the
    // order matches the real hierarchy.
    public static class GarageContentGenerator
    {
        private const string RootFolder = "Assets/GameAssets/ScriptableObjects";
        private const string CarFolder = RootFolder + "/Cars";
        private const string CatalogPath = RootFolder + "/CarCatalog.asset";
        private const string PalettePath = RootFolder + "/CarColorPalette.asset";
        private const string UpgradeRulesPath = RootFolder + "/UpgradeRules.asset";
        private const string QualityProfilePath = RootFolder + "/QualityProfile.asset";

        private struct CarSpec
        {
            public string id;
            public string name;
            public int price;
            public int carIndex;
            public int topSpeed;
            public int acceleration;
            public int braking;
            public int handling;
        }

        [MenuItem("Tools/Car Parking/Generate Garage Content")]
        public static void Generate()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(CarFolder);

            var definitions = new List<CarDefinition>();

            foreach (CarSpec spec in BuildCarSpecs())
            {
                string path = $"{CarFolder}/{spec.id}.asset";
                CarDefinition definition = LoadOrCreate<CarDefinition>(path);

                definition.EditorConfigure(
                    spec.id,
                    spec.name,
                    spec.price,
                    spec.carIndex,
                    spec.topSpeed,
                    spec.acceleration,
                    spec.braking,
                    spec.handling);

                EditorUtility.SetDirty(definition);
                definitions.Add(definition);
            }

            CarCatalog catalog = LoadOrCreate<CarCatalog>(CatalogPath);
            catalog.EditorSetCars(definitions);
            EditorUtility.SetDirty(catalog);

            CarColorPalette palette = LoadOrCreate<CarColorPalette>(PalettePath);
            palette.EditorSetColors(BuildPalette());
            EditorUtility.SetDirty(palette);

            LoadOrCreate<UpgradeRules>(UpgradeRulesPath);
            LoadOrCreate<QualityProfile>(QualityProfilePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[GarageContentGenerator] Wrote {definitions.Count} car definitions, the catalog, the colour palette, upgrade rules and the quality profile under '{RootFolder}'.");
        }

        public static void GenerateFromCommandLine()
        {
            Generate();
            EditorApplication.Exit(0);
        }

        [MenuItem("Tools/Car Parking/Validate Garage")]
        public static void ValidateGarage()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>(CatalogPath);

            if (catalog == null)
            {
                Debug.LogError($"[GarageContentGenerator] No car catalog at '{CatalogPath}'. Run Generate Garage Content first.");
                return;
            }

            var issues = new List<string>();
            var seenIndices = new HashSet<int>();

            foreach (CarDefinition car in catalog.Cars)
            {
                if (car == null)
                {
                    issues.Add("The catalog contains an empty slot.");
                    continue;
                }

                if (car.CarIndex < 0)
                {
                    issues.Add($"'{car.DisplayName}' has a negative car index.");
                }

                if (!seenIndices.Add(car.CarIndex))
                {
                    issues.Add($"Car index {car.CarIndex} is used by more than one car definition.");
                }
            }

            var garage = Object.FindFirstObjectByType<GarageManager>();

            if (garage == null || garage.PlayerCarContainer == null)
            {
                Debug.LogWarning("[GarageContentGenerator] No GarageManager with a player-car container in the open scene, so car indices could not be checked against the real hierarchy. Open the gameplay scene and set one up to complete this check.");
            }
            else
            {
                Transform container = garage.PlayerCarContainer.transform;

                foreach (CarDefinition car in catalog.Cars)
                {
                    if (car == null)
                    {
                        continue;
                    }

                    if (car.CarIndex >= container.childCount)
                    {
                        issues.Add($"'{car.DisplayName}' points at child index {car.CarIndex} but '{container.name}' only has {container.childCount} children.");
                        continue;
                    }

                    Debug.Log($"[GarageContentGenerator] Car index {car.CarIndex} ('{car.DisplayName}') maps to scene object '{container.GetChild(car.CarIndex).name}'. Confirm that is the right car.");
                }
            }

            if (issues.Count == 0)
            {
                Debug.Log($"[GarageContentGenerator] Validated {catalog.Count} cars with no structural issues.");
                return;
            }

            foreach (string issue in issues)
            {
                Debug.LogWarning("[GarageContentGenerator] " + issue);
            }
        }

        private static CarSpec[] BuildCarSpecs()
        {
            return new[]
            {
                new CarSpec { id = "classic_red", name = "Classic", price = 0, carIndex = 0, topSpeed = 3, acceleration = 3, braking = 3, handling = 3 },
                new CarSpec { id = "hot_rod", name = "Hot Rod", price = 3000, carIndex = 1, topSpeed = 4, acceleration = 4, braking = 3, handling = 3 },
                new CarSpec { id = "muscle", name = "Muscle", price = 6000, carIndex = 2, topSpeed = 5, acceleration = 4, braking = 3, handling = 2 }
            };
        }

        private static List<CarColorOption> BuildPalette()
        {
            return new List<CarColorOption>
            {
                new CarColorOption { displayName = "Red", color = new Color(0.72f, 0.12f, 0.12f) },
                new CarColorOption { displayName = "Blue", color = new Color(0.13f, 0.32f, 0.68f) },
                new CarColorOption { displayName = "Green", color = new Color(0.16f, 0.5f, 0.28f) },
                new CarColorOption { displayName = "Yellow", color = new Color(0.9f, 0.72f, 0.16f) },
                new CarColorOption { displayName = "Orange", color = new Color(0.88f, 0.45f, 0.12f) },
                new CarColorOption { displayName = "White", color = new Color(0.92f, 0.92f, 0.92f) },
                new CarColorOption { displayName = "Grey", color = new Color(0.4f, 0.42f, 0.45f) },
                new CarColorOption { displayName = "Black", color = new Color(0.1f, 0.1f, 0.11f) }
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

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(lastSlash + 1));
        }
    }
}
