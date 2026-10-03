using CarParkingGame.Core;
using CarParkingGame.Progression;
using CarParkingGame.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace CarParkingGame.UI
{
    public class SettingsView : MonoBehaviour
    {
        [SerializeField] private SettingsManager settings;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private Button lowQualityButton;
        [SerializeField] private Button mediumQualityButton;
        [SerializeField] private Button highQualityButton;
        [SerializeField] private Button frameRate30Button;
        [SerializeField] private Button frameRate60Button;
        [SerializeField] private Text qualityLabel;
        [SerializeField] private Text frameRateLabel;
        [SerializeField] private Button resetProgressButton;
        [SerializeField] private GameObject resetConfirmPanel;
        [SerializeField] private Button resetConfirmButton;
        [SerializeField] private Button resetCancelButton;

        private SettingsManager Settings => settings != null ? settings : SettingsManager.Instance;

        private void Awake()
        {
            musicSlider?.onValueChanged.AddListener(value => Settings?.SetMusicVolume(value));
            sfxSlider?.onValueChanged.AddListener(value => Settings?.SetSfxVolume(value));
            sensitivitySlider?.onValueChanged.AddListener(value => Settings?.SetSteeringSensitivity(value));

            lowQualityButton?.onClick.AddListener(() => SetTier(GraphicsTier.Low));
            mediumQualityButton?.onClick.AddListener(() => SetTier(GraphicsTier.Medium));
            highQualityButton?.onClick.AddListener(() => SetTier(GraphicsTier.High));

            frameRate30Button?.onClick.AddListener(() => SetFrameRate(30));
            frameRate60Button?.onClick.AddListener(() => SetFrameRate(60));

            resetProgressButton?.onClick.AddListener(() => ShowResetConfirm(true));
            resetCancelButton?.onClick.AddListener(() => ShowResetConfirm(false));
            resetConfirmButton?.onClick.AddListener(ConfirmReset);
        }

        private void OnEnable()
        {
            ShowResetConfirm(false);
            Refresh();
        }

        public void Refresh()
        {
            SettingsSaveData saved = SaveManager.Data.settings;

            SetSliderWithoutNotify(musicSlider, saved.musicVolume);
            SetSliderWithoutNotify(sfxSlider, saved.sfxVolume);
            SetSliderWithoutNotify(sensitivitySlider, saved.steeringSensitivity);

            if (qualityLabel != null)
            {
                qualityLabel.text = ((GraphicsTier)Mathf.Clamp(saved.graphicsQuality, 0, 2)).ToString().ToUpperInvariant();
            }

            if (frameRateLabel != null)
            {
                frameRateLabel.text = $"{saved.targetFrameRate} FPS";
            }
        }

        private static void SetSliderWithoutNotify(Slider slider, float value)
        {
            // Avoids the slider's own callback writing the same value back to the save file.
            slider?.SetValueWithoutNotify(value);
        }

        private void SetTier(GraphicsTier tier)
        {
            Settings?.SetGraphicsTier(tier);
            Refresh();
        }

        private void SetFrameRate(int frameRate)
        {
            Settings?.SetTargetFrameRate(frameRate);
            Refresh();
        }

        private void ShowResetConfirm(bool show)
        {
            if (resetConfirmPanel != null)
            {
                resetConfirmPanel.SetActive(show);
            }
        }

        private void ConfirmReset()
        {
            Settings?.ResetProgress();
            ShowResetConfirm(false);
            Refresh();
        }
    }
}
