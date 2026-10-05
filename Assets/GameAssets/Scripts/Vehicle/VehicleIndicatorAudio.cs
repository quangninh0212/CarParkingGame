using CarParkingGame.Settings;
using UnityEngine;

namespace CarParkingGame.Vehicle
{
    // The ticking a car makes while an indicator is on.
    //
    // It follows the lamps rather than the button: the sound has to start when the
    // indicator starts and stop the moment it is cancelled, and the lamps are what know
    // that. Cancelling happens by pressing the other side too, not only by pressing the
    // same one again.
    //
    // The clip is a continuous recording of an indicator ticking, nineteen seconds of it,
    // so it is looped for as long as the indicator is on rather than fired once per flash.
    // Starting it on every flash would stack nineteen-second clips on top of each other
    // within a second of switching on.
    [RequireComponent(typeof(VehicleLights))]
    public class VehicleIndicatorAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip tick;
        [SerializeField] private AudioSource source;

        [Tooltip("How loud the tick is with the effects slider at full.")]
        [SerializeField, Range(0f, 1f)] private float tickVolume = 0.55f;

        private VehicleLights lights;

        private void Awake()
        {
            lights = GetComponent<VehicleLights>();
        }

        private void OnDisable()
        {
            Silence();
        }

        private void Update()
        {
            if (lights == null || source == null || tick == null)
            {
                return;
            }

            if (lights.Indicator == IndicatorSide.None)
            {
                Silence();
                return;
            }

            source.volume = tickVolume * GameAudio.Sfx;

            if (source.isPlaying)
            {
                return;
            }

            // From the top each time it is switched on, so the first tick lands with the
            // first flash rather than wherever the loop happened to be.
            source.clip = tick;
            source.loop = true;
            source.time = 0f;
            source.Play();
        }

        private void Silence()
        {
            if (source != null && source.isPlaying)
            {
                source.Stop();
            }
        }
    }
}
