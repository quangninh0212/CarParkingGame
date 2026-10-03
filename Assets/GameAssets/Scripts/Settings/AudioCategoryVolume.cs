using CarParkingGame.Core;
using UnityEngine;

namespace CarParkingGame.Settings
{
    public enum AudioCategory
    {
        Music,
        Sfx
    }

    // Put this on any AudioSource that should follow the player's music or SFX slider.
    // The project has no AudioMixer, and creating one with exposed parameters cannot be
    // done reliably from a script, so volume is applied per source instead.
    [RequireComponent(typeof(AudioSource))]
    public class AudioCategoryVolume : MonoBehaviour
    {
        [SerializeField] private AudioCategory category = AudioCategory.Sfx;
        [SerializeField, Range(0f, 1f)] private float baseVolume = 1f;

        private AudioSource source;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SettingsApplied += Apply;
            }

            Apply();
        }

        private void OnDisable()
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SettingsApplied -= Apply;
            }
        }

        public void Apply()
        {
            float categoryVolume = category == AudioCategory.Music
                ? SaveManager.Data.settings.musicVolume
                : SaveManager.Data.settings.sfxVolume;

            source.volume = baseVolume * categoryVolume;
        }
    }
}
