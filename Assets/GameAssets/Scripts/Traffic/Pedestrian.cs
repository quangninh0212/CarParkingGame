using UnityEngine;

namespace CarParkingGame.Traffic
{
    // Walks a waypoint path, pauses now and then, and holds back when a vehicle is close -
    // enough to read as a living street without any crowd simulation. Ticked by
    // PedestrianManager, and its vehicle check runs on a staggered timer.
    public class Pedestrian : MonoBehaviour
    {
        [SerializeField] private float walkSpeed = 1.3f;
        [SerializeField] private float turnDegreesPerSecond = 220f;
        [SerializeField] private float waypointReachedDistance = 0.6f;
        [SerializeField] private Vector2 pauseSecondsRange = new Vector2(1f, 4f);
        [SerializeField] private Vector2 secondsBetweenPausesRange = new Vector2(8f, 18f);
        [SerializeField] private float vehicleCheckRadius = 2f;
        [SerializeField] private float crossingCheckRadius = 4.5f;
        [SerializeField] private LayerMask vehicleMask = ~0;
        [SerializeField] private Animator animator;

        [Tooltip("Optional animator float set to the current walking speed. Leave empty to drive no animator.")]
        [SerializeField] private string animatorSpeedParameter = "Speed";

        private const float VehicleCheckInterval = 0.2f;

        private WaypointPath path;
        private int waypointIndex;
        private float pauseRemaining;
        private float untilNextPause;
        private float vehicleCheckTimer;
        private bool vehicleTooClose;

        public bool HasPath => path != null && path.IsUsable;

        public void Initialize(WaypointPath startPath, int startIndex, float checkPhase)
        {
            path = startPath;
            waypointIndex = startIndex;
            pauseRemaining = 0f;
            untilNextPause = Random.Range(secondsBetweenPausesRange.x, secondsBetweenPausesRange.y);
            vehicleCheckTimer = checkPhase;
            vehicleTooClose = false;

            Transform waypoint = path != null ? path.At(startIndex) : null;

            if (waypoint != null)
            {
                transform.position = waypoint.position;
            }
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

            UpdateVehicleCheck(deltaTime, IsAtCrossing());

            bool waiting = vehicleTooClose || UpdatePauseTimers(deltaTime);
            float speed = waiting ? 0f : walkSpeed;

            ReportSpeedToAnimator(speed);

            if (waiting)
            {
                return;
            }

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.magnitude <= waypointReachedDistance)
            {
                AdvanceWaypoint();
                return;
            }

            Quaternion wanted = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, turnDegreesPerSecond * deltaTime);
            transform.position += transform.forward * (speed * deltaTime);
        }

        // Returns true while the pedestrian is standing still.
        private bool UpdatePauseTimers(float deltaTime)
        {
            if (pauseRemaining > 0f)
            {
                pauseRemaining -= deltaTime;
                return true;
            }

            untilNextPause -= deltaTime;

            if (untilNextPause > 0f)
            {
                return false;
            }

            pauseRemaining = Random.Range(pauseSecondsRange.x, pauseSecondsRange.y);
            untilNextPause = Random.Range(secondsBetweenPausesRange.x, secondsBetweenPausesRange.y);
            return true;
        }

        private void UpdateVehicleCheck(float deltaTime, bool atCrossing)
        {
            vehicleCheckTimer -= deltaTime;

            if (vehicleCheckTimer > 0f)
            {
                return;
            }

            vehicleCheckTimer = VehicleCheckInterval;

            float radius = atCrossing ? crossingCheckRadius : vehicleCheckRadius;
            Vector3 ahead = transform.position + Vector3.up * 0.5f + transform.forward * (radius * 0.5f);

            vehicleTooClose = Physics.CheckSphere(ahead, radius, vehicleMask, QueryTriggerInteraction.Ignore);
        }

        private bool IsAtCrossing()
        {
            TrafficWaypoint data = path.WaypointDataAt(waypointIndex);
            return data != null && data.IsCrossing;
        }

        private void ReportSpeedToAnimator(float speed)
        {
            if (animator == null || string.IsNullOrEmpty(animatorSpeedParameter))
            {
                return;
            }

            animator.SetFloat(animatorSpeedParameter, speed);
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

            waypointIndex = 0;
        }
    }
}
