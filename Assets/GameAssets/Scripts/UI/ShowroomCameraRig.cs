using CarParkingGame.Vehicle;
using UnityEngine;

namespace CarParkingGame.UI
{
    public enum ShowroomFocus
    {
        Home,
        Garage
    }

    // Frames the showroom car for the menu camera, and turns slowly around it.
    //
    // The garage screen showed no car at all because the panel was an opaque full-screen
    // rectangle in front of a camera that was not pointed at anything in particular. The
    // fix is not a render texture: the showroom car is already standing in the world, so
    // the camera is aimed at it and the garage panel leaves one side of the screen clear.
    //
    // Framing is computed from the car's own bounds, so the three cars - which differ in
    // length by more than a metre - all fill the same amount of the screen.
    public class ShowroomCameraRig : MonoBehaviour
    {
        [SerializeField] private Transform menuCamera;

        [Tooltip("Container whose active child is the car on show.")]
        [SerializeField] private Transform showroomCars;

        // Both framings look from the same side. The other side of the showroom has a
        // grandstand and a lamp post a couple of metres from the car, and orbiting round
        // to it put the camera inside them.
        [Header("Home framing")]
        [SerializeField] private float homeYaw = 125f;
        [SerializeField] private float homePitch = 9f;
        [SerializeField] private float homeDistanceFactor = 2.5f;

        [Header("Garage framing")]
        [SerializeField] private float garageYaw = 125f;
        [SerializeField] private float garagePitch = 9f;
        [SerializeField] private float garageDistanceFactor = 2.3f;

        [Header("Composition")]
        [Tooltip("Both screens put a panel down the left, so the car is framed right of centre.")]
        [SerializeField] private float screenOffset = 0.26f;

        [Tooltip("Aims below the car so it sits above the middle of the screen rather than under it.")]
        [SerializeField] private float aimDrop = 0.3f;

        [Header("Motion")]
        [SerializeField] private float orbitDegreesPerSecond = 6f;
        [SerializeField] private float moveSmoothing = 6f;

        private ShowroomFocus focus = ShowroomFocus.Home;
        private float orbit;

        public void SetFocus(ShowroomFocus next)
        {
            if (focus == next)
            {
                return;
            }

            focus = next;
            orbit = 0f;
        }

        // Jumps straight to the framing instead of easing into it. Used when the menu
        // opens, so the first frame is already composed, and by the screenshot tool.
        public void FrameImmediately(ShowroomFocus next)
        {
            focus = next;
            orbit = 0f;
            Frame(1f);
        }

        private void LateUpdate()
        {
            // Unscaled: the menu runs on a stopped clock.
            orbit += orbitDegreesPerSecond * Time.unscaledDeltaTime;
            Frame(1f - Mathf.Exp(-moveSmoothing * Time.unscaledDeltaTime));
        }

        private void Frame(float blend)
        {
            if (menuCamera == null || showroomCars == null)
            {
                return;
            }

            Transform car = ActiveChild();

            if (car == null)
            {
                return;
            }

            bool garage = focus == ShowroomFocus.Garage;
            float yaw = (garage ? garageYaw : homeYaw) + orbit;
            float pitch = garage ? garagePitch : homePitch;
            float factor = garage ? garageDistanceFactor : homeDistanceFactor;

            float radius = MeasureRadius(car);
            float distance = Mathf.Max(3f, radius * factor);

            Vector3 pivot = car.position + Vector3.up * (radius * 0.35f);
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 desired = pivot + rotation * new Vector3(0f, 0f, -distance);

            menuCamera.position = Vector3.Lerp(menuCamera.position, desired, blend);

            // Aiming off centre is what leaves room for the side panel without moving the
            // car or the UI: aim left of the car and the car sits right of centre.
            Vector3 aim = pivot
                          + menuCamera.right * (distance * screenOffset)
                          - Vector3.up * (radius * aimDrop);

            Quaternion look = Quaternion.LookRotation(aim - menuCamera.position, Vector3.up);
            menuCamera.rotation = Quaternion.Slerp(menuCamera.rotation, look, blend);
        }

        private Transform ActiveChild()
        {
            for (int i = 0; i < showroomCars.childCount; i++)
            {
                Transform child = showroomCars.GetChild(i);

                if (child.gameObject.activeSelf)
                {
                    return child;
                }
            }

            return null;
        }

        private static float MeasureRadius(Transform car)
        {
            return VehicleViewPoints.TryMeasureBodyBounds(car, out Bounds local)
                ? Mathf.Max(1.5f, local.extents.magnitude)
                : 3f;
        }
    }
}
