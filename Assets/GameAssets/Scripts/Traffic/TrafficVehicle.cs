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

        // How close to a waypoint under a red light counts as being on the line.
        private const float StopLineHold = 1.2f;

        private WaypointPath path;
        private int waypointIndex;
        private float currentSpeedKmh;
        private float sensorTimer;
        private bool blockedAhead;
        private Rigidbody body;
        private float rideHeight;
        private float noseAhead;

        // Enough room for one car and a gap. Built once and reused, so a sensor sweep does
        // not allocate every time it runs.
        private readonly RaycastHit[] sensorHits = new RaycastHit[8];

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;

            if (sensorOrigin == null)
            {
                sensorOrigin = transform;
            }

            Measure();
        }

        // Where this model's wheels and nose are relative to its pivot.
        //
        // The car packs do not agree on where a prefab's pivot is - some have it on the
        // floor, some in the middle of the body, and the nose is anywhere from half a metre
        // to three metres forward of it. Both of those have to be measured rather than
        // assumed: a guessed ride height buried half the cars in the road, and a guessed
        // sensor origin started the sweep inside the car's own bodywork.
        private void Measure()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            rideHeight = Mathf.Max(0f, transform.position.y - bounds.min.y);

            // The furthest corner of the car in the direction it faces, which is as far
            // forward as any part of it reaches however the pivot is placed.
            Vector3 local = transform.InverseTransformPoint(bounds.max);
            Vector3 other = transform.InverseTransformPoint(bounds.min);

            noseAhead = Mathf.Max(Mathf.Abs(local.z), Mathf.Abs(other.z)) + 0.15f;
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
                waypoint.position + Vector3.up * rideHeight,
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

            TrafficWaypoint data = path.WaypointDataAt(waypointIndex);
            bool held = data != null && data.MustStop;
            float distance = toTarget.magnitude;

            // Standing on the line. Nothing moves and nothing turns, so a car waiting on a
            // red cannot creep past the waypoint and then swing round to come back to it.
            if (held && distance <= StopLineHold)
            {
                currentSpeedKmh = 0f;
                return;
            }

            // A waypoint under a red light is not reached, however close the car gets.
            //
            // Without this, a car braking for a red crossed into the two and a half metres
            // that count as arriving, took the next waypoint - which has no light on it -
            // and drove away through the junction. It stopped for nothing: the light only
            // ever slowed it down.
            if (!held && distance <= waypointReachedDistance)
            {
                AdvanceWaypoint();
                return;
            }

            Steer(toTarget, deltaTime);
            Drive(distance, deltaTime, held);
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

        private void Drive(float distanceToWaypoint, float deltaTime, bool held)
        {
            float targetSpeed = ResolveTargetSpeed(distanceToWaypoint, held);

            currentSpeedKmh = targetSpeed > currentSpeedKmh
                ? Mathf.MoveTowards(currentSpeedKmh, targetSpeed, accelerationKmhPerSecond * deltaTime)
                : Mathf.MoveTowards(currentSpeedKmh, targetSpeed, brakingKmhPerSecond * deltaTime);

            if (currentSpeedKmh <= 0.01f)
            {
                return;
            }

            transform.position += transform.forward * (currentSpeedKmh / 3.6f * deltaTime);
        }

        private float ResolveTargetSpeed(float distanceToWaypoint, bool held)
        {
            if (blockedAhead)
            {
                return 0f;
            }

            TrafficWaypoint data = path.WaypointDataAt(waypointIndex);

            float limit = data != null && data.SpeedLimitKmh > 0f ? data.SpeedLimitKmh : cruiseSpeedKmh;
            float speed = Mathf.Min(limit, cruiseSpeedKmh);

            if (!held || distanceToWaypoint > stopLineDistance)
            {
                return speed;
            }

            // Metered down over the last few metres instead of cut to nothing at the stop
            // line. Cruise speed needs about five metres to shed, so dropping the target to
            // zero at exactly five left the car still rolling when it got there; this has
            // it arriving stopped.
            float range = Mathf.Max(0.1f, stopLineDistance - StopLineHold);
            float share = Mathf.Clamp01((distanceToWaypoint - StopLineHold) / range);

            return speed * share;
        }

        private void UpdateSensor(float deltaTime)
        {
            sensorTimer -= deltaTime;

            if (sensorTimer > 0f)
            {
                return;
            }

            sensorTimer = SensorInterval;

            // From the nose, not the pivot. A sweep that starts inside the car's own
            // bodywork comes back having hit the car itself at zero distance, and since
            // that hit is thrown away as being its own, the sweep reported a clear road
            // whatever was standing in it. That is why traffic drove through parked cars.
            Vector3 origin = sensorOrigin.position + transform.forward * noseAhead + Vector3.up * 0.2f;

            int count = Physics.SphereCastNonAlloc(
                origin,
                sensorRadius,
                transform.forward,
                sensorHits,
                sensorLength,
                obstacleMask,
                QueryTriggerInteraction.Ignore);

            blockedAhead = false;

            for (int i = 0; i < count; i++)
            {
                if (IsSomethingToStopFor(sensorHits[i].collider))
                {
                    blockedAhead = true;
                    return;
                }
            }
        }

        // Only cars. Traffic runs a fixed loop and is never going to wander into the
        // scenery, so treating walls and kerbs as obstacles only ever gave false stops -
        // a car swinging round a corner points at the car park wall for a moment and would
        // sit there for good.
        private bool IsSomethingToStopFor(Collider collider)
        {
            if (collider == null || collider.transform.IsChildOf(transform))
            {
                return false;
            }

            return collider.GetComponentInParent<TrafficVehicle>() != null
                || collider.GetComponentInParent<CarController>() != null;
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
