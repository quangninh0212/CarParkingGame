using UnityEngine;

namespace CarParkingGame.Vehicle
{
    // The noise the car makes when the brake is stood on at speed.
    //
    // It listens for the brake being *held*, not for CarController.IsBraking, which is
    // also true whenever the car coasts with no throttle - on that the sound would play
    // every time the player let go of the accelerator.
    //
    // One shot per stop rather than a loop: the clip is a single stopping sound, and
    // looping one of those gives a car that screeches forever while it waits at a kerb. It
    // is faded out instead when the brake comes off or the car is down to walking pace, so
    // a long clip does not keep going after the car has stopped.
    [RequireComponent(typeof(CarController))]
    public class VehicleBrakeAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip brakeClip;
        [SerializeField] private AudioSource source;

        [Tooltip("Below this the car is nudging rather than braking, and stopping is silent.")]
        [SerializeField] private float needsKmh = 10f;

        [Tooltip("Speed at which the brake is at full volume.")]
        [SerializeField] private float loudKmh = 45f;

        [Tooltip("Once the car is down to this, the sound fades out - it has stopped.")]
        [SerializeField] private float finishedKmh = 5f;

        [Tooltip("Seconds before another stop can make a noise. Longer than the clip, or holding the brake restarts it the moment it ends and the car stutters.")]
        [SerializeField] private float retriggerSeconds = 1.6f;

        [SerializeField] private float fadeOutPerSecond = 3.5f;

        private CarController car;
        private float quietUntil;
        private float level;

        private void Awake()
        {
            car = GetComponent<CarController>();
        }

        private void Reset()
        {
            source = GetComponent<AudioSource>();
        }

        private void Update()
        {
            if (car == null || source == null || brakeClip == null)
            {
                return;
            }

            float speed = car.CurrentSpeedKmh;
            bool braking = car.IsBrakeHeld && car.VehicleEnabled;

            if (braking && speed >= needsKmh && Time.unscaledTime >= quietUntil && !source.isPlaying)
            {
                quietUntil = Time.unscaledTime + retriggerSeconds;

                level = Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(needsKmh, loudKmh, speed));

                source.clip = brakeClip;
                source.volume = level;
                source.Play();
                return;
            }

            if (!source.isPlaying)
            {
                return;
            }

            // Still braking and still moving: leave it alone.
            if (braking && speed > finishedKmh)
            {
                return;
            }

            // Faded rather than cut, because cutting a stopping sound mid-way is a click.
            level -= fadeOutPerSecond * Time.deltaTime;

            if (level <= 0f)
            {
                source.Stop();
                level = 0f;
                return;
            }

            source.volume = level;
        }
    }
}
