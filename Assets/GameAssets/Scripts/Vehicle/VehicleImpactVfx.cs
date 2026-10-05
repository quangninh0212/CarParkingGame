using UnityEngine;

namespace CarParkingGame.Vehicle
{
    // Sparks where the car hits something, at the point it hit.
    //
    // Spawned from a prefab and left to destroy itself, rather than pooled: a parking game
    // produces a collision every few seconds at worst, and a pool for that is machinery
    // nobody needs. The same impulse that decides how loud the bang is decides whether
    // there is a spark at all, so a kerb being leaned on does not shower the car park.
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleImpactVfx : MonoBehaviour
    {
        [SerializeField] private GameObject sparkPrefab;

        [Tooltip("Impulse below this is the car settling against something, and makes no spark.")]
        [SerializeField] private float needsImpulse = 2.5f;

        [Tooltip("Seconds before another impact can spark. One collision reports several contacts.")]
        [SerializeField] private float retriggerSeconds = 0.25f;

        [SerializeField] private float effectSeconds = 3f;

        private float quietUntil;

        private void OnCollisionEnter(Collision collision)
        {
            if (sparkPrefab == null || Time.unscaledTime < quietUntil)
            {
                return;
            }

            if (collision.impulse.magnitude < needsImpulse || collision.contactCount == 0)
            {
                return;
            }

            quietUntil = Time.unscaledTime + retriggerSeconds;

            ContactPoint contact = collision.GetContact(0);

            // Facing back out of whatever was hit, so the sparks fly away from the surface
            // rather than into it.
            GameObject spark = Instantiate(
                sparkPrefab,
                contact.point,
                Quaternion.LookRotation(contact.normal, Vector3.up));

            Destroy(spark, effectSeconds);
        }
    }
}
