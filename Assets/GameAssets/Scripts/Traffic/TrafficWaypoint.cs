using UnityEngine;

namespace CarParkingGame.Traffic
{
    // Optional extra data on a waypoint: a speed limit, a traffic light that governs it,
    // or a crossing where pedestrians may step onto the road.
    public class TrafficWaypoint : MonoBehaviour
    {
        [Tooltip("0 keeps the vehicle's own cruise speed.")]
        [SerializeField] private float speedLimitKmh;

        [SerializeField] private TrafficLight governingLight;
        [SerializeField] private bool isCrossing;

        public float SpeedLimitKmh => speedLimitKmh;
        public TrafficLight GoverningLight => governingLight;
        public bool IsCrossing => isCrossing;

        public bool MustStop => governingLight != null && governingLight.RequiresStop;

        private void OnDrawGizmos()
        {
            if (governingLight != null)
            {
                Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.8f);
                Gizmos.DrawLine(transform.position, governingLight.transform.position);
            }

            if (!isCrossing)
            {
                return;
            }

            Gizmos.color = new Color(1f, 1f, 1f, 0.6f);
            Gizmos.DrawWireCube(transform.position, new Vector3(3f, 0.1f, 1f));
        }
    }
}
