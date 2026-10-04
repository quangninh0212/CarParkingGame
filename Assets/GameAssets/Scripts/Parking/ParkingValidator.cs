using System;
using UnityEngine;

namespace CarParkingGame.Parking
{
    public enum ParkingState
    {
        Outside,
        Partial,
        Holding,
        Complete
    }

    // Replaces "a tagged collider touched a trigger" with a real parking check:
    // the car has to sit inside the bay, point the right way, be virtually stopped,
    // and stay that way for a moment. Every threshold is pushed in per mission by
    // MissionManager via Configure().
    public class ParkingValidator : MonoBehaviour
    {
        [SerializeField] private ParkingZone zone;
        [SerializeField] private ParkingType parkingType = ParkingType.Forward;
        [SerializeField] private float maxSpeedKmh = 2f;
        [SerializeField] private float angleToleranceDegrees = 12f;
        [SerializeField] private float holdSeconds = 1.5f;
        [SerializeField] private float requiredContainment = 1f;
        [SerializeField] private bool allowOppositeHeading = true;
        [SerializeField] private Vector2 fallbackHalfExtents = new Vector2(0.9f, 2.1f);

        private const float ReverseEntrySpeed = 0.3f;
        private const float WheelOverhang = 0.35f;

        private Transform vehicle;
        private Rigidbody vehicleBody;
        private Vector2 halfExtents;
        private float holdTimer;
        private float reportedProgress = -1f;
        private bool reversedIntoZone;
        private bool running;
        private ParkingState state = ParkingState.Outside;

        public event Action<ParkingState> StateChanged;
        public event Action<float> ProgressChanged;
        public event Action Validated;

        public ParkingZone Zone
        {
            get => zone;
            set => zone = value;
        }

        public ParkingState State => state;
        public bool IsComplete => state == ParkingState.Complete;
        public bool IsRunning => running;

        public float Progress => holdSeconds <= 0f
            ? (state == ParkingState.Complete ? 1f : 0f)
            : Mathf.Clamp01(holdTimer / holdSeconds);

        public float LastContainment { get; private set; }
        public float LastHeadingError { get; private set; } = 180f;
        public float LastSpeedKmh { get; private set; }

        public void Configure(
            ParkingType type,
            float maxSpeed,
            float angleTolerance,
            float hold,
            float containment,
            bool allowOpposite)
        {
            parkingType = type;
            maxSpeedKmh = Mathf.Max(0.1f, maxSpeed);
            angleToleranceDegrees = Mathf.Clamp(angleTolerance, 1f, 90f);
            holdSeconds = Mathf.Max(0f, hold);
            requiredContainment = Mathf.Clamp01(containment);
            allowOppositeHeading = allowOpposite;
        }

        public void SetVehicle(Transform vehicleTransform)
        {
            vehicle = vehicleTransform;
            vehicleBody = vehicleTransform != null ? vehicleTransform.GetComponent<Rigidbody>() : null;
            halfExtents = MeasureFootprint(vehicleTransform, fallbackHalfExtents);
        }

        public void Begin()
        {
            holdTimer = 0f;
            reportedProgress = -1f;
            reversedIntoZone = false;
            running = true;
            SetState(ParkingState.Outside);
            WarnIfBayCannotFitVehicle();
        }

        // The legacy parking triggers were small boxes at the centre of a bay. If one of
        // those is reused as the bay itself, full containment is geometrically impossible
        // and the mission would silently never complete, so say so loudly instead.
        private void WarnIfBayCannotFitVehicle()
        {
            if (zone == null || requiredContainment < 1f)
            {
                return;
            }

            float neededWidth = halfExtents.x * 2f;
            float neededLength = halfExtents.y * 2f;
            Vector3 bay = zone.Size;

            if (bay.x >= neededWidth && bay.z >= neededLength)
            {
                return;
            }

            Debug.LogWarning(
                $"[ParkingValidator] Bay '{zone.name}' is {bay.x:0.0} x {bay.z:0.0} but this car needs " +
                $"{neededWidth:0.0} x {neededLength:0.0} to sit fully inside it. With required containment at 1 " +
                "the mission cannot be completed - enlarge the ParkingZone or lower the mission's required containment.",
                zone);
        }

#if UNITY_EDITOR
        // Lets the editor's "park for me" shortcut complete reverse missions, which
        // otherwise require the car to have actually driven backwards into the bay.
        public void EditorMarkReversedIn()
        {
            reversedIntoZone = true;
        }
#endif

        public void Stop()
        {
            running = false;
            holdTimer = 0f;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        // Public so the validation state machine can be driven with an explicit time step
        // from tests, instead of only from a running player loop.
        public void Tick(float deltaTime)
        {
            if (!running || vehicle == null || zone == null)
            {
                return;
            }

            Evaluate(deltaTime);
        }

        private void Evaluate(float deltaTime)
        {
            LastContainment = zone.GetContainment(vehicle, halfExtents);
            LastSpeedKmh = vehicleBody != null ? vehicleBody.linearVelocity.magnitude * 3.6f : 0f;

            bool opposite = allowOppositeHeading || parkingType == ParkingType.Parallel;
            LastHeadingError = zone.GetHeadingError(vehicle, opposite);

            if (parkingType == ParkingType.Reverse && vehicleBody != null && LastContainment >= 0.5f)
            {
                float forwardSpeed = Vector3.Dot(vehicleBody.linearVelocity, vehicle.forward);

                if (forwardSpeed < -ReverseEntrySpeed)
                {
                    reversedIntoZone = true;
                }
            }

            bool inside = LastContainment >= requiredContainment - 0.001f;
            // A bay with no arrow painted in it takes the car whichever way round it ends
            // up, so there is nothing to be aligned with.
            bool aligned = !zone.RequireHeading || LastHeadingError <= angleToleranceDegrees;
            bool stopped = LastSpeedKmh <= maxSpeedKmh;
            bool enteredCorrectly = parkingType != ParkingType.Reverse || reversedIntoZone;

            if (inside && aligned && stopped && enteredCorrectly)
            {
                holdTimer += deltaTime;
                SetState(holdTimer >= holdSeconds ? ParkingState.Complete : ParkingState.Holding);
            }
            else
            {
                holdTimer = 0f;
                SetState(LastContainment > 0f ? ParkingState.Partial : ParkingState.Outside);
            }

            ReportProgress();

            if (state == ParkingState.Complete)
            {
                running = false;
                Validated?.Invoke();
            }
        }

        private void SetState(ParkingState next)
        {
            if (state == next)
            {
                return;
            }

            state = next;
            StateChanged?.Invoke(state);
        }

        private void ReportProgress()
        {
            float progress = Progress;

            if (Mathf.Abs(progress - reportedProgress) < 0.01f)
            {
                return;
            }

            reportedProgress = progress;
            ProgressChanged?.Invoke(progress);
        }

        // Wheel positions describe the car's footprint without needing a hand-authored
        // box per vehicle; the overhang accounts for bodywork sitting outside the wheels.
        private static Vector2 MeasureFootprint(Transform vehicleTransform, Vector2 fallback)
        {
            if (vehicleTransform == null)
            {
                return fallback;
            }

            var controller = vehicleTransform.GetComponent<CarController>();

            if (controller == null || controller.wheels == null || controller.wheels.Count == 0)
            {
                return fallback;
            }

            float maxX = 0f;
            float maxZ = 0f;

            foreach (CarController.Wheel wheel in controller.wheels)
            {
                if (wheel.wheelCollider == null)
                {
                    continue;
                }

                Vector3 local = vehicleTransform.InverseTransformPoint(wheel.wheelCollider.transform.position);
                maxX = Mathf.Max(maxX, Mathf.Abs(local.x));
                maxZ = Mathf.Max(maxZ, Mathf.Abs(local.z));
            }

            if (maxX <= 0.01f || maxZ <= 0.01f)
            {
                return fallback;
            }

            return new Vector2(maxX + WheelOverhang * 0.5f, maxZ + WheelOverhang);
        }
    }
}
