using System;
using CarParkingGame.Core;
using CarParkingGame.Progression;
using UnityEngine;

namespace CarParkingGame.Garage
{
    // Owning, selecting, painting and upgrading cars. Keeps the existing three cars and
    // the existing container-child layout: ownership is keyed by child index, which is how
    // CarSelection has always identified cars.
    public class GarageManager : MonoBehaviour
    {
        public static GarageManager Instance { get; private set; }

        [SerializeField] private CarCatalog catalog;
        [SerializeField] private CarColorPalette palette;
        [SerializeField] private UpgradeRules upgradeRules;

        [Tooltip("Container whose children are the drivable cars.")]
        [SerializeField] private GameObject playerCarContainer;

        [Tooltip("Optional showroom container that mirrors the same car order.")]
        [SerializeField] private GameObject showroomCarContainer;

        [Tooltip("Also write the legacy 'SelectedCarIndex' PlayerPrefs key, so the old " +
                 "CarSelection components stay in step until they are removed from the scene.")]
        [SerializeField] private bool keepLegacyCarSelectionInSync = true;

        private const string LegacySelectedCarKey = "SelectedCarIndex";

        private int previewPosition;

        public event Action PreviewChanged;
        public event Action GarageChanged;

        public CarCatalog Catalog => catalog;
        public CarColorPalette Palette => palette;
        public UpgradeRules Upgrades => upgradeRules;
        public int PreviewPosition => previewPosition;
        public CarDefinition PreviewCar => catalog != null ? catalog.At(previewPosition) : null;

        // Exposed so the garage validation tool can check the catalog's car indices
        // against the containers' actual children.
        public GameObject PlayerCarContainer => playerCarContainer;
        public GameObject ShowroomCarContainer => showroomCarContainer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            if (catalog == null || catalog.Count == 0)
            {
                Debug.LogError("[GarageManager] No car catalog assigned; the garage cannot show anything.", this);
                return;
            }

            CarDefinition selected = catalog.FindByCarIndex(SaveManager.Data.selectedCarIndex);
            previewPosition = selected != null ? IndexOf(selected) : 0;

            ApplyPreviewToShowroom();
            ApplySelectedCarToGameplay();
        }

        public void ShowNext()
        {
            Step(1);
        }

        public void ShowPrevious()
        {
            Step(-1);
        }

        public bool IsOwned(CarDefinition car)
        {
            if (car == null)
            {
                return false;
            }

            if (car.OwnedByDefault)
            {
                return true;
            }

            CarSaveEntry entry = SaveManager.Data.FindCar(car.CarIndex);
            return entry != null && entry.owned;
        }

        public bool IsPreviewSelected
        {
            get
            {
                CarDefinition car = PreviewCar;
                return car != null && SaveManager.Data.selectedCarIndex == car.CarIndex;
            }
        }

        public bool CanAffordPreview()
        {
            CarDefinition car = PreviewCar;
            return car != null && SaveManager.Data.coins >= car.PurchasePrice;
        }

        public bool BuyPreviewCar()
        {
            CarDefinition car = PreviewCar;

            if (car == null || IsOwned(car))
            {
                return false;
            }

            if (!EconomyManager.TrySpend(car.PurchasePrice))
            {
                return false;
            }

            SaveManager.Data.GetOrCreateCar(car.CarIndex).owned = true;
            SaveManager.Save();

            GarageChanged?.Invoke();
            return true;
        }

        public bool SelectPreviewCar()
        {
            CarDefinition car = PreviewCar;

            if (car == null || !IsOwned(car))
            {
                return false;
            }

            SaveManager.Data.selectedCarIndex = car.CarIndex;
            SaveManager.Save();

            if (keepLegacyCarSelectionInSync)
            {
                PlayerPrefs.SetInt(LegacySelectedCarKey, car.CarIndex);
                PlayerPrefs.Save();
            }

            ApplySelectedCarToGameplay();
            GarageChanged?.Invoke();
            return true;
        }

        public void SetPreviewColor(int colorIndex)
        {
            CarDefinition car = PreviewCar;

            if (car == null || palette == null)
            {
                return;
            }

            SaveManager.Data.GetOrCreateCar(car.CarIndex).colorIndex = colorIndex;
            SaveManager.Save();

            ApplyPreviewToShowroom();

            if (SaveManager.Data.selectedCarIndex == car.CarIndex)
            {
                ApplySelectedCarToGameplay();
            }

            GarageChanged?.Invoke();
        }

        public int GetUpgradeLevel(UpgradeKind kind)
        {
            CarDefinition car = PreviewCar;

            return car == null ? 0 : VehicleUpgradeService.GetLevel(SaveManager.Data.FindCar(car.CarIndex), kind);
        }

        public int GetUpgradeCost(UpgradeKind kind)
        {
            return upgradeRules == null ? 0 : upgradeRules.CostForNextLevel(GetUpgradeLevel(kind));
        }

        public bool UpgradePreviewCar(UpgradeKind kind)
        {
            CarDefinition car = PreviewCar;

            if (car == null || upgradeRules == null || !IsOwned(car))
            {
                return false;
            }

            CarSaveEntry entry = SaveManager.Data.GetOrCreateCar(car.CarIndex);

            if (!VehicleUpgradeService.TryUpgrade(entry, kind, upgradeRules, out int _))
            {
                return false;
            }

            if (SaveManager.Data.selectedCarIndex == car.CarIndex)
            {
                ApplySelectedCarToGameplay();
            }

            GarageChanged?.Invoke();
            return true;
        }

        private void Step(int direction)
        {
            if (catalog == null || catalog.Count == 0)
            {
                return;
            }

            previewPosition = (previewPosition + direction + catalog.Count) % catalog.Count;

            ApplyPreviewToShowroom();
            PreviewChanged?.Invoke();
        }

        private int IndexOf(CarDefinition car)
        {
            for (int i = 0; i < catalog.Count; i++)
            {
                if (catalog.At(i) == car)
                {
                    return i;
                }
            }

            return 0;
        }

        private void ApplyPreviewToShowroom()
        {
            CarDefinition car = PreviewCar;

            if (car == null || showroomCarContainer == null)
            {
                return;
            }

            GameObject shown = ActivateOnlyChild(showroomCarContainer, car.CarIndex);
            PaintCar(shown, car);
        }

        private void ApplySelectedCarToGameplay()
        {
            if (playerCarContainer == null)
            {
                return;
            }

            int selectedIndex = SaveManager.Data.selectedCarIndex;
            CarDefinition car = catalog != null ? catalog.FindByCarIndex(selectedIndex) : null;

            GameObject active = ActivateOnlyChild(playerCarContainer, selectedIndex);

            if (active == null)
            {
                return;
            }

            PaintCar(active, car);

            var controller = active.GetComponent<CarController>();

            if (controller != null)
            {
                VehicleUpgradeService.Apply(controller, SaveManager.Data.FindCar(selectedIndex), upgradeRules);
            }
        }

        private void PaintCar(GameObject car, CarDefinition definition)
        {
            if (car == null || definition == null || palette == null)
            {
                return;
            }

            CarSaveEntry entry = SaveManager.Data.FindCar(definition.CarIndex);
            int colorIndex = entry != null ? entry.colorIndex : CarSaveEntry.DefaultColor;

            // A default colour index means "leave the car's own materials alone".
            if (!palette.TryGetColor(colorIndex, out Color color))
            {
                return;
            }

            CarPaintTarget paint = car.GetComponentInChildren<CarPaintTarget>(true);
            paint?.Apply(color);
        }

        private static GameObject ActivateOnlyChild(GameObject container, int childIndex)
        {
            Transform parent = container.transform;

            if (childIndex < 0 || childIndex >= parent.childCount)
            {
                Debug.LogError($"[GarageManager] '{container.name}' has no child at index {childIndex}; the car catalog and the scene disagree.", container);
                return null;
            }

            GameObject target = null;

            for (int i = 0; i < parent.childCount; i++)
            {
                GameObject child = parent.GetChild(i).gameObject;
                bool isTarget = i == childIndex;

                child.SetActive(isTarget);

                if (isTarget)
                {
                    target = child;
                }
            }

            return target;
        }
    }
}
