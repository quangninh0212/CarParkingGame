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

    // Cycles between the existing follow camera, a cockpit view and a rear/reversing view.
    //
    // Third person is not reimplemented: the existing CarCameraController stays in charge
    // of it, so that view behaves exactly as it does today. The other two modes park the
    // camera rigidly on the car, which is what an interior or reversing view should do.
    public class VehicleCameraDirector : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private CarCameraController legacyFollowCamera;
        [SerializeField] private Vector3 cockpitOffset = new Vector3(0.35f, 1.1f, 0.15f);
        [SerializeField] private Vector3 rearOffset = new Vector3(0f, 1.5f, -2.6f);
        [SerializeField] private float rearPitchDegrees = 8f;

        private VehicleCameraMode mode = VehicleCameraMode.ThirdPerson;

        public VehicleCameraMode Mode => mode;

        public event Action<VehicleCameraMode> ModeChanged;

        private void Awake()
        {
            if (cameraTransform == null && legacyFollowCamera != null)
            {
                cameraTransform = legacyFollowCamera.transform;
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

        private void ApplyMode()
        {
            if (legacyFollowCamera != null)
            {
                legacyFollowCamera.enabled = mode == VehicleCameraMode.ThirdPerson;
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

            if (mode == VehicleCameraMode.Cockpit)
            {
                cameraTransform.SetPositionAndRotation(body.TransformPoint(cockpitOffset), body.rotation);
                return;
            }

            Vector3 position = body.TransformPoint(rearOffset);
            Quaternion lookBackwards = Quaternion.LookRotation(-body.forward, Vector3.up);

            cameraTransform.SetPositionAndRotation(
                position,
                lookBackwards * Quaternion.Euler(rearPitchDegrees, 0f, 0f));
        }
    }
}
