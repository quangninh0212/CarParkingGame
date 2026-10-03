using CarParkingGame.Parking;
using UnityEngine;

namespace CarParkingGame.OpenWorld
{
    // A place in the open world where a challenge can be started. Proximity is not tested
    // here: OpenWorldChallengeManager scans all markers on a throttled timer, so adding
    // markers does not add Update calls.
    public class ChallengeMarker : MonoBehaviour
    {
        [SerializeField] private ChallengeDefinition definition;
        [SerializeField] private ParkingZone parkingZone;
        [SerializeField] private float activationRadius = 12f;

        [Tooltip("Optional floating icon, hidden while the challenge is running.")]
        [SerializeField] private GameObject worldIcon;

        public ChallengeDefinition Definition => definition;
        public ParkingZone ParkingZone => parkingZone;
        public float ActivationRadius => activationRadius;

        public bool IsConfigured => definition != null && parkingZone != null;

        public float DistanceTo(Vector3 worldPosition)
        {
            return Vector3.Distance(transform.position, worldPosition);
        }

        public bool IsWithinRange(Vector3 worldPosition)
        {
            return DistanceTo(worldPosition) <= activationRadius;
        }

        public void SetIconVisible(bool visible)
        {
            if (worldIcon != null)
            {
                worldIcon.SetActive(visible);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, activationRadius);

            if (parkingZone != null)
            {
                Gizmos.DrawLine(transform.position, parkingZone.WorldCenter);
            }
        }
    }
}
