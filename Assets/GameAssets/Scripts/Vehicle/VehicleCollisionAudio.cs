using CarParkingGame.Settings;
using UnityEngine;

namespace CarParkingGame.Vehicle
{
    // The noise the car makes when it hits something.
    //
    // One clip, with the impulse deciding how loud and how deep it plays. A light kerb
    // and a wall hitting the same sample at the same volume is what makes a collision
    // sound canned, and pitch carries most of the difference.
    //
    // This listens for collisions itself rather than going through
    // VehicleCollisionReporter. The reporter is added to the car at runtime by whichever
    // mission is being played, so it does not exist in free roam, in the menu showroom, or
    // between missions - and a car that only makes a noise during a scored mission sounds
    // broken everywhere else.
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleCollisionAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip impact;
        [SerializeField] private AudioSource source;

        [Tooltip("Impulse below this is the car settling against something it is already touching, and makes no noise.")]
        [SerializeField] private float quietBelow = 0.6f;

        [Tooltip("Impulse at or above this plays the heavy hit at full volume.")]
        [SerializeField] private float loudAt = 9f;

        [Tooltip("Seconds before the same car can make another impact noise. One collision reports several contacts.")]
        [SerializeField] private float retriggerSeconds = 0.13f;

        private float quietUntil;

        private void Reset()
        {
            source = GetComponent<AudioSource>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            Play(collision);
        }

        // Scraping along a wall never fires another Enter, so without this a car held
        // against a barrier is silent while it grinds down it.
        private void OnCollisionStay(Collision collision)
        {
            // Three metres a second and up. Below that the car is leaning on something
            // rather than grinding along it, and a noise every tenth of a second would be
            // a rattle rather than a scrape.
            if (collision.relativeVelocity.sqrMagnitude > 9f)
            {
                Play(collision);
            }
        }

        private void Play(Collision collision)
        {
            if (source == null || Time.unscaledTime < quietUntil)
            {
                return;
            }

            float impulse = collision.impulse.magnitude;

            if (impulse < quietBelow)
            {
                return;
            }

            if (impact == null)
            {
                return;
            }

            quietUntil = Time.unscaledTime + retriggerSeconds;

            // Loud hits are both louder and a little deeper, which is most of what tells a
            // scrape apart from a crash without needing a clip for each.
            float weight = Mathf.InverseLerp(quietBelow, loudAt, impulse);

            source.pitch = Mathf.Lerp(1.18f, 0.82f, weight) + Random.Range(-0.04f, 0.04f);
            source.PlayOneShot(impact, Mathf.Lerp(0.25f, 1f, weight) * GameAudio.Sfx);
        }
    }
}
