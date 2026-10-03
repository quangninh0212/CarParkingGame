using System;
using CarParkingGame.Core;
using CarParkingGame.Garage;
using UnityEngine;
using UnityEngine.UI;

namespace CarParkingGame.UI
{
    [Serializable]
    public class UpgradeRow
    {
        public UpgradeKind kind = UpgradeKind.Engine;
        public Button button;
        public Text levelLabel;
        public Text costLabel;
    }

    public class GarageView : MonoBehaviour
    {
        [SerializeField] private GarageManager garage;
        [SerializeField] private Text carNameLabel;
        [SerializeField] private Text coinLabel;
        [SerializeField] private Text priceLabel;
        [SerializeField] private Text statsLabel;
        [SerializeField] private Image thumbnail;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button buyButton;
        [SerializeField] private Button selectButton;
        [SerializeField] private Text selectButtonLabel;
        [SerializeField] private GameObject lockedBadge;
        [SerializeField] private GameObject selectedBadge;
        [SerializeField] private Button[] colorButtons;
        [SerializeField] private UpgradeRow[] upgradeRows;

        private GarageManager Garage => garage != null ? garage : GarageManager.Instance;

        private void Awake()
        {
            previousButton?.onClick.AddListener(() => Step(-1));
            nextButton?.onClick.AddListener(() => Step(1));
            buyButton?.onClick.AddListener(OnBuy);
            selectButton?.onClick.AddListener(OnSelect);

            for (int i = 0; i < (colorButtons?.Length ?? 0); i++)
            {
                int colorIndex = i;
                colorButtons[i]?.onClick.AddListener(() => OnColor(colorIndex));
            }

            PaintSwatches();

            foreach (UpgradeRow row in upgradeRows ?? Array.Empty<UpgradeRow>())
            {
                UpgradeRow captured = row;
                captured.button?.onClick.AddListener(() => OnUpgrade(captured.kind));
            }
        }

        private void OnEnable()
        {
            GarageManager garageManager = Garage;

            if (garageManager != null)
            {
                garageManager.PreviewChanged += Refresh;
                garageManager.GarageChanged += Refresh;
            }

            Refresh();
        }

        private void OnDisable()
        {
            GarageManager garageManager = Garage;

            if (garageManager != null)
            {
                garageManager.PreviewChanged -= Refresh;
                garageManager.GarageChanged -= Refresh;
            }
        }

        public void Refresh()
        {
            GarageManager garageManager = Garage;

            if (garageManager == null)
            {
                Debug.LogError("[GarageView] No GarageManager available.", this);
                return;
            }

            CarDefinition car = garageManager.PreviewCar;

            if (car == null)
            {
                return;
            }

            bool owned = garageManager.IsOwned(car);
            bool selected = garageManager.IsPreviewSelected;

            if (carNameLabel != null)
            {
                carNameLabel.text = car.DisplayName;
            }

            if (coinLabel != null)
            {
                coinLabel.text = SaveManager.Data.coins.ToString();
            }

            // The garage used to show a bare number and a dead button, so "I can't press
            // anything" and "this costs more than I have" looked identical. Every state now
            // says what it is.
            if (priceLabel != null)
            {
                if (owned)
                {
                    priceLabel.text = selected ? "YOU ARE DRIVING THIS" : "OWNED";
                }
                else
                {
                    int shortfall = car.PurchasePrice - SaveManager.Data.coins;

                    priceLabel.text = shortfall > 0
                        ? $"{car.PurchasePrice} coins - {shortfall} more needed"
                        : $"{car.PurchasePrice} coins";
                }
            }

            if (selectButtonLabel != null)
            {
                selectButtonLabel.text = selected ? "DRIVING" : "DRIVE";
            }

            if (statsLabel != null)
            {
                statsLabel.text =
                    $"Speed {Bar(car.TopSpeedRating)}\nAccel {Bar(car.AccelerationRating)}\n" +
                    $"Brake {Bar(car.BrakingRating)}\nGrip  {Bar(car.HandlingRating)}";
            }

            if (thumbnail != null)
            {
                thumbnail.sprite = car.Thumbnail;
                thumbnail.enabled = car.Thumbnail != null;
            }

            if (buyButton != null)
            {
                buyButton.gameObject.SetActive(!owned);
                buyButton.interactable = !owned && garageManager.CanAffordPreview();
            }

            if (selectButton != null)
            {
                selectButton.gameObject.SetActive(owned);
                selectButton.interactable = owned && !selected;
            }

            if (lockedBadge != null)
            {
                lockedBadge.SetActive(!owned);
            }

            if (selectedBadge != null)
            {
                selectedBadge.SetActive(selected);
            }

            RefreshUpgradeRows(garageManager, owned);
        }

        private void RefreshUpgradeRows(GarageManager garageManager, bool owned)
        {
            if (upgradeRows == null)
            {
                return;
            }

            int maxLevel = garageManager.Upgrades != null ? garageManager.Upgrades.MaxLevel : 0;

            foreach (UpgradeRow row in upgradeRows)
            {
                if (row == null)
                {
                    continue;
                }

                int level = garageManager.GetUpgradeLevel(row.kind);
                int cost = garageManager.GetUpgradeCost(row.kind);
                bool maxed = level >= maxLevel;

                if (row.levelLabel != null)
                {
                    row.levelLabel.text = $"{level}/{maxLevel}";
                }

                if (row.costLabel != null)
                {
                    row.costLabel.text = maxed ? "MAX" : cost.ToString();
                }

                if (row.button != null)
                {
                    row.button.interactable = owned && !maxed && SaveManager.Data.coins >= cost;
                }
            }
        }

        // Each swatch shows the colour it applies. They were built as plain white discs and
        // nothing ever coloured them, so the garage offered eight identical buttons.
        private void PaintSwatches()
        {
            GarageManager garageManager = Garage;
            CarColorPalette palette = garageManager != null ? garageManager.Palette : null;

            if (colorButtons == null || palette == null)
            {
                return;
            }

            for (int i = 0; i < colorButtons.Length; i++)
            {
                Button swatch = colorButtons[i];

                if (swatch == null)
                {
                    continue;
                }

                bool hasColor = palette.TryGetColor(i, out Color color);
                swatch.gameObject.SetActive(hasColor);

                if (hasColor && swatch.targetGraphic != null)
                {
                    swatch.targetGraphic.color = color;
                }
            }
        }

        private static string Bar(int rating)
        {
            return new string('|', Mathf.Clamp(rating, 1, 5));
        }

        private void Step(int direction)
        {
            if (direction > 0)
            {
                Garage?.ShowNext();
            }
            else
            {
                Garage?.ShowPrevious();
            }

            Refresh();
        }

        private void OnBuy()
        {
            Garage?.BuyPreviewCar();
            Refresh();
        }

        private void OnSelect()
        {
            Garage?.SelectPreviewCar();
            Refresh();
        }

        private void OnColor(int colorIndex)
        {
            Garage?.SetPreviewColor(colorIndex);
            Refresh();
        }

        private void OnUpgrade(UpgradeKind kind)
        {
            Garage?.UpgradePreviewCar(kind);
            Refresh();
        }
    }
}
