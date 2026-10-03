using System;
using UnityEngine;

namespace CarParkingGame.Vehicle
{
    public enum VehicleCameraMode
    {
        ThirdPerson,
        Cockpit,
        Rear
    }

    // Cycles between the existing follow camera, a driver's-seat view and a
    // driver's-seat view turned to look out of the back window.
    //
    // Both seated views share one eye point - the driver's head - because that is what
    // "look forward" and "look behind" mean in a car. The previous version put the
    // forward view on a fixed offset near the bonnet and the reversing view on a boom
    // behind the boot, so neither read as sitting in the car.
    //
    // Third person is not reimplemented: CarCameraController stays in charge of it, so
    // that view behaves exactly as it does today.
    public class VehicleCameraDirector : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private CarCameraController legacyFollowCamera;

        [Tooltip("The driver's view looks slightly down, the way a driver does.")]
        [SerializeField] private float seatedPitchDegrees = 4f;

        [Tooltip("Looking back is aimed further down: what matters behind you is the ground.")]
        [SerializeField] private float rearPitchDegrees = 14f;

        [Tooltip("Near clip while seated, so the car's own bodywork does not fill the view.")]
        [SerializeField] private float seatedNearClip = 0.05f;

        private VehicleCameraMode mode = VehicleCameraMode.ThirdPerson;
        private Camera cameraComponent;
        private float authoredNearClip = 0.3f;
        private CarController measuredCar;
        private Vector3 eyeLocalPosition = new Vector3(-0.35f, 1.1f, 0.6f);
        private Vector3 rearLocalPosition = new Vector3(0f, 1.2f, -0.6f);

        public VehicleCameraMode Mode => mode;

        public event Action<VehicleCameraMode> ModeChanged;

        private void Awake()
        {
            if (cameraTransform == null && legacyFollowCamera != null)
            {
                cameraTransform = legacyFollowCamera.transform;
            }

            if (cameraTransform != null)
            {
                cameraComponent = cameraTransform.GetComponent<Camera>();
            }

            if (cameraComponent != null)
            {
                authoredNearClip = cameraComponent.nearClipPlane;
            }

            ApplyMode();
        }

        public void CycleMode()
        {
            switch (mode)
            {
                case VehicleCameraMode.ThirdPerson:
                    SetMode(VehicleCameraMode.Cockpit);
                    break;
                case VehicleCameraMode.Cockpit:
                    SetMode(VehicleCameraMode.Rear);
                    break;
                default:
                    SetMode(VehicleCameraMode.ThirdPerson);
                    break;
            }
        }

        public void SetMode(VehicleCameraMode next)
        {
            if (mode == next)
            {
                return;
            }

            mode = next;
            ApplyMode();
            ModeChanged?.Invoke(mode);
        }

        public string ModeCaption => mode switch
        {
            VehicleCameraMode.Cockpit => "DRIVER",
            VehicleCameraMode.Rear => "LOOK BACK",
            _ => "FOLLOW"
        };

        private void ApplyMode()
        {
            bool thirdPerson = mode == VehicleCameraMode.ThirdPerson;

            if (legacyFollowCamera != null)
            {
                legacyFollowCamera.enabled = thirdPerson;
            }

            if (cameraComponent != null)
            {
                cameraComponent.nearClipPlane = thirdPerson ? authoredNearClip : seatedNearClip;
            }
        }

        private void LateUpdate()
        {
            if (mode == VehicleCameraMode.ThirdPerson || cameraTransform == null)
            {
                return;
            }

            CarController car = ActiveVehicleLocator.Current;

            if (car == null)
            {
                return;
            }

            Transform body = car.transform;
            bool rear = mode == VehicleCameraMode.Rear;

            Measure(car);

            // Yawed 180 degrees for the rear view rather than aimed with LookRotation:
            // turning in place is what a driver does, and it keeps roll tied to the car.
            Quaternion facing = body.rotation * Quaternion.Euler(
                rear ? rearPitchDegrees : seatedPitchDegrees,
                rear ? 180f : 0f,
                0f);

            cameraTransform.SetPositionAndRotation(
                body.TransformPoint(rear ? rearLocalPosition : eyeLocalPosition),
                facing);
        }

        private void Measure(CarController car)
        {
            if (measuredCar == car)
            {
                return;
            }

            measuredCar = car;

            var points = car.GetComponent<VehicleViewPoints>();

            if (points == null)
            {
                points = car.gameObject.AddComponent<VehicleViewPoints>();
                points.Measure();
            }

            eyeLocalPosition = points.DriverEyeLocalPosition;
            rearLocalPosition = points.RearViewLocalPosition;
        }
    }
}
