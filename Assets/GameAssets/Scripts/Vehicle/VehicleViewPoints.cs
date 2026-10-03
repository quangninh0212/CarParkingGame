using UnityEngine;

namespace CarParkingGame.Vehicle
{
    // Where the driver's head is on a given car, in the car's own local space.
    //
    // Measured from the car's own body bounds rather than hand-authored per car: the
    // three cars have different lengths, widths and ride heights, and a shared offset
    // put the camera in the engine bay on one and behind the rear seats on another.
    [RequireComponent(typeof(CarController))]
    public class VehicleViewPoints : MonoBehaviour
    {
        [Tooltip("Driver's eye point in the car's local space. Left blank, it is measured on Awake.")]
        [SerializeField] private Vector3 driverEyeLocalPosition;

        [SerializeField] private bool measured;

        [Tooltip("Fraction of the body's half width the driver sits off centre. Negative is left-hand drive.")]
        [SerializeField] private float seatSideFraction = -0.42f;

        [Tooltip("Fraction of the body's length, from its centre, the driver sits behind the middle.")]
        [SerializeField] private float seatLengthFraction = -0.06f;

        [Tooltip("Fraction of the body's height the eye point sits above the body's centre.")]
        [SerializeField] private float eyeHeightFraction = 0.22f;

        public Vector3 DriverEyeLocalPosition => driverEyeLocalPosition;

        private void Awake()
        {
            if (!measured)
            {
                Measure();
            }
        }

        [ContextMenu("Measure From Body")]
        public void Measure()
        {
            if (!TryMeasureBodyBounds(transform, out Bounds local))
            {
                driverEyeLocalPosition = new Vector3(-0.35f, 1.1f, 0.1f);
                measured = true;
                return;
            }

            driverEyeLocalPosition = new Vector3(
                local.center.x + local.extents.x * seatSideFraction,
                local.center.y + local.extents.y * eyeHeightFraction,
                local.center.z + local.extents.z * seatLengthFraction);

            measured = true;
        }

        // The car's own renderers minus wheels and the placeholder lamps, expressed in the
        // car's local space. World-space Renderer.bounds is axis-aligned, so it is folded
        // back through the car's transform instead of being used directly.
        public static bool TryMeasureBodyBounds(Transform car, out Bounds localBounds)
        {
            localBounds = new Bounds();
            bool any = false;

            foreach (Renderer renderer in car.GetComponentsInChildren<Renderer>(true))
            {
                if (IsExcluded(renderer.transform, car))
                {
                    continue;
                }

                var filter = renderer.GetComponent<MeshFilter>();

                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                Bounds mesh = filter.sharedMesh.bounds;
                Matrix4x4 toCar = car.worldToLocalMatrix * renderer.transform.localToWorldMatrix;

                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3(
                        (corner & 1) == 0 ? mesh.min.x : mesh.max.x,
                        (corner & 2) == 0 ? mesh.min.y : mesh.max.y,
                        (corner & 4) == 0 ? mesh.min.z : mesh.max.z);

                    Vector3 inCar = toCar.MultiplyPoint3x4(point);

                    if (!any)
                    {
                        localBounds = new Bounds(inCar, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(inCar);
                    }
                }
            }

            return any;
        }

        private static bool IsExcluded(Transform candidate, Transform car)
        {
            for (Transform t = candidate; t != null && t != car.parent; t = t.parent)
            {
                string name = t.name;

                if (name.IndexOf("wheel", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("PlaceholderLights", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
