using UnityEngine;

namespace CarParkingGame.Traffic
{
    // A traffic car. Kinematic on purpose: WheelCollider physics for a dozen background
    // vehicles would cost far more than it adds, and the player's own car is still a
    // dynamic body, so collisions with it behave normally.
    //
    // Ticked by TrafficManager instead of having its own Update, and its obstacle sensor
    // runs on a staggered timer rather than every frame.
    [RequireComponent(typeof(Rigidbody))]
    public class TrafficVehicle : MonoBehaviour
    {
        [SerializeField] private float cruiseSpeedKmh = 30f;
        [SerializeField] private float accelerationKmhPerSecond = 8f;
        [SerializeField] private float brakingKmhPerSecond = 25f;
        [SerializeField] private float turnDegreesPerSecond = 110f;
        [SerializeField] private float waypointReachedDistance = 2.5f;
        [SerializeField] private float sensorLength = 7f;
        [SerializeField] private float sensorRadius = 1f;
        [SerializeField] private float stopLineDistance = 5f;
        [SerializeField] private LayerMask obstacleMask = ~0;
        [SerializeField] private Transform sensorOrigin;

        private const float SensorInterval = 0.15f;

        private WaypointPath path;
        private int waypointIndex;
        private float currentSpeedKmh;
        private float sensorTimer;
        private bool blockedAhead;
        private Rigidbody body;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;

            if (sensorOrigin == null)
            {
                sensorOrigin = transform;
            }
        }

        public bool HasPath => path != null && path.IsUsable;

        public void Initialize(WaypointPath startPath, int startIndex, float sensorPhase)
        {
            path = startPath;
            waypointIndex = startIndex;
            currentSpeedKmh = 0f;
            blockedAhead = false;
            sensorTimer = sensorPhase;

            Transform waypoint = path != null ? path.At(startIndex) : null;

            if (waypoint == null)
            {
                return;
            }

            Transform next = path.At(path.NextIndex(startIndex));
            Vector3 facing = next != null ? next.position - waypoint.position : waypoint.forward;
            facing.y = 0f;

            transform.SetPositionAndRotation(
                waypoint.position,
                facing.sqrMagnitude > 0.001f ? Quaternion.LookRotation(facing) : waypoint.rotation);
        }

        public void Tick(float deltaTime)
        {
            if (!HasPath)
            {
                return;
            }

            Transform target = path.At(waypointIndex);

            if (target == null)
            {
                AdvanceWaypoint();
                return;
            }

            UpdateSensor(deltaTime);

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.magnitude <= waypointReachedDistance)
            {
                AdvanceWaypoint();
                return;
            }

            Steer(toTarget, deltaTime);
            Drive(toTarget.magnitude, deltaTime);
        }

        private void Steer(Vector3 toTarget, float deltaTime)
        {
            if (toTarget.sqrMagnitude < 0.001f)
            {
                return;
            }

            Quaternion wanted = Quaternion.LookRotation(toTarget.normalized, Vector3.up);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                wanted,
                turnDegreesPerSecond * deltaTime);
        }

        private void Drive(float distanceToWaypoint, float deltaTime)
        {
            float targetSpeed = ResolveTargetSpeed(distanceToWaypoint);

            currentSpeedKmh = targetSpeed > currentSpeedKmh
                ? Mathf.MoveTowards(currentSpeedKmh, targetSpeed, accelerationKmhPerSecond * deltaTime)
                : Mathf.MoveTowards(currentSpeedKmh, targetSpeed, brakingKmhPerSecond * deltaTime);

            if (currentSpeedKmh <= 0.01f)
            {
                return;
            }

            transform.position += transform.forward * (currentSpeedKmh / 3.6f * deltaTime);
        }

        private float ResolveTargetSpeed(float distanceToWaypoint)
        {
            if (blockedAhead)
            {
                return 0f;
            }

            TrafficWaypoint data = path.WaypointDataAt(waypointIndex);

            if (data != null && data.MustStop && distanceToWaypoint <= stopLineDistance)
            {
                return 0f;
            }

            float limit = data != null && data.SpeedLimitKmh > 0f ? data.SpeedLimitKmh : cruiseSpeedKmh;
            return Mathf.Min(limit, cruiseSpeedKmh);
        }

        private void UpdateSensor(float deltaTime)
        {
            sensorTimer -= deltaTime;

            if (sensorTimer > 0f)
            {
                return;
            }

            sensorTimer = SensorInterval;

            Vector3 origin = sensorOrigin.position + Vector3.up * 0.2f;

            blockedAhead = Physics.SphereCast(
                origin,
                sensorRadius,
                transform.forward,
                out RaycastHit hit,
                sensorLength,
                obstacleMask,
                QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform);
        }

        private void AdvanceWaypoint()
        {
            int next = path.NextIndex(waypointIndex);

            if (next >= 0)
            {
                waypointIndex = next;
                return;
            }

            WaypointPath continuation = path.PickContinuation();

            if (continuation != null)
            {
                path = continuation;
                waypointIndex = 0;
                return;
            }

            // Dead end with nowhere to go: turn around and use the path backwards.
            waypointIndex = 0;
        }
    }
}
