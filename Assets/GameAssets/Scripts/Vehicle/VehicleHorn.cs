using CarParkingGame.Core;
using UnityEngine;

namespace CarParkingGame.Vehicle
{
    // One AudioSource, created once and reused, so holding the horn button down cannot
    // spawn a source per press.
    [RequireComponent(typeof(CarController))]
    public class VehicleHorn : MonoBehaviour
    {
        [SerializeField] private AudioClip hornClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
        [SerializeField] private float spatialBlend = 1f;
        [SerializeField] private float maxDistance = 60f;

        private AudioSource source;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.clip = hornClip;
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = spatialBlend;
            source.maxDistance = maxDistance;
        }

        public void Play()
        {
            if (hornClip == null)
            {
                return;
            }

            source.volume = volume * SaveManager.Data.settings.sfxVolume;

            if (!source.isPlaying)
            {
                source.Play();
            }
        }

        public void Stop()
        {
            source.Stop();
        }
    }
}
