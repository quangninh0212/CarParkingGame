using System;
using CarParkingGame.Core;
using CarParkingGame.Progression;
using CarParkingGame.Vehicle;
using UnityEngine;

namespace CarParkingGame.Settings
{
    // Applies the saved settings at startup and whenever the player changes one.
    // Works without a QualityProfile asset assigned (it falls back to sane defaults), so a
    // missing reference degrades instead of breaking the game.
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        // The ends of the camera speed slider. Shared with the screen that draws it so the
        // slider cannot offer a value the setting would then clamp away.
        public const float MinCameraSensitivity = 0.4f;
        public const float MaxCameraSensitivity = 3f;

        [SerializeField] private QualityProfile qualityProfile;

        private QualityProfile profile;

        public event Action SettingsApplied;

        public GraphicsTier Tier => (GraphicsTier)Mathf.Clamp(
            SaveManager.Data.settings.graphicsQuality,
            SaveData.MinGraphicsQuality,
            SaveData.MaxGraphicsQuality);

        public GraphicsTierSettings CurrentTierSettings => profile.For(Tier);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            profile = qualityProfile != null ? qualityProfile : QualityProfile.CreateDefault();

            ApplyAll();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void ApplyAll()
        {
            SettingsSaveData settings = SaveManager.Data.settings;

            ApplyGraphics(profile.For(Tier));
            ApplyFrameRate(settings.targetFrameRate);
            VehicleInput.SteeringSensitivity = settings.steeringSensitivity;
            CarCameraController.SensitivityScale = settings.cameraSensitivity;

            SettingsApplied?.Invoke();
        }

        public void SetGraphicsTier(GraphicsTier tier)
        {
            SaveManager.Data.settings.graphicsQuality = (int)tier;
            SaveManager.Save();
            ApplyAll();
        }

        public void SetTargetFrameRate(int frameRate)
        {
            SaveManager.Data.settings.targetFrameRate = frameRate <= 45 ? 30 : 60;
            SaveManager.Save();
            ApplyAll();
        }

        public void SetMusicVolume(float volume)
        {
            SaveManager.Data.settings.musicVolume = Mathf.Clamp01(volume);
            SaveManager.Save();
            SettingsApplied?.Invoke();
        }

        public void SetSfxVolume(float volume)
        {
            SaveManager.Data.settings.sfxVolume = Mathf.Clamp01(volume);
            SaveManager.Save();
            SettingsApplied?.Invoke();
        }

        public void SetCameraSensitivity(float sensitivity)
        {
            float clamped = Mathf.Clamp(sensitivity, MinCameraSensitivity, MaxCameraSensitivity);

            SaveManager.Data.settings.cameraSensitivity = clamped;
            CarCameraController.SensitivityScale = clamped;
            SaveManager.Save();
            SettingsApplied?.Invoke();
        }

        public void SetSteeringSensitivity(float sensitivity)
        {
            float clamped = Mathf.Clamp(sensitivity, 0.25f, 3f);

            SaveManager.Data.settings.steeringSensitivity = clamped;
            VehicleInput.SteeringSensitivity = clamped;
            SaveManager.Save();
            SettingsApplied?.Invoke();
        }

        public void ResetProgress()
        {
            SaveManager.ResetProgress();
            ApplyAll();
        }

        private static void ApplyGraphics(GraphicsTierSettings tier)
        {
            QualitySettings.shadows = tier.shadowsEnabled
                ? (tier.softShadows ? ShadowQuality.All : ShadowQuality.HardOnly)
                : ShadowQuality.Disable;

            QualitySettings.shadowDistance = tier.shadowDistance;
            QualitySettings.shadowResolution = tier.shadowResolution;
            QualitySettings.lodBias = tier.lodBias;
        }

        // vSync has to be off or Android ignores targetFrameRate entirely.
        private static void ApplyFrameRate(int frameRate)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = frameRate <= 45 ? 30 : 60;
        }
    }
}
