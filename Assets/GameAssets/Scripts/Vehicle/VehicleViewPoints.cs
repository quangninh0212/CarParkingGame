using UnityEngine;

namespace CarParkingGame.Vehicle
{
    // Where the two seated cameras sit on a given car, in the car's own local space.
    //
    // Measured from the car's own body bounds rather than hand-authored per car: the three
    // cars differ in length by more than a metre and in ride height by a third of a metre,
    // and a shared offset put the camera under the dashboard on one and through the roof
    // on another.
    //
    // Looking forward and looking back are two separate points, not one point turned
    // round. Spun on the spot from the driver's seat, the back of the cabin fills the whole
    // view - which is exactly what the first version did, and why the rear view was black.
    [RequireComponent(typeof(CarController))]
    public class VehicleViewPoints : MonoBehaviour
    {
        [SerializeField] private Vector3 driverEyeLocalPosition;
        [SerializeField] private Vector3 rearViewLocalPosition;
        [SerializeField] private bool measured;

        [Header("Driver's seat")]
        [Tooltip("Fraction of the body's half width the driver sits off centre. Negative is left-hand drive.")]
        [SerializeField] private float seatSideFraction = -0.42f;

        [Tooltip("Fraction of the body's half length ahead of its middle, so the view clears the bonnet.")]
        [SerializeField] private float seatLengthFraction = 0.3f;

        [Tooltip("Fraction of the body's height, measured from its floor, that the eye sits at.")]
        [SerializeField] private float eyeHeightFraction = 0.7f;

        [Header("Looking back")]
        [Tooltip("Fraction of the body's half length behind its middle.")]
        [SerializeField] private float rearLengthFraction = -0.45f;

        [Tooltip("Fraction of the body's height for the rear view; above the boot line.")]
        [SerializeField] private float rearHeightFraction = 1f;

        public Vector3 DriverEyeLocalPosition => driverEyeLocalPosition;
        public Vector3 RearViewLocalPosition => rearViewLocalPosition;

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
                driverEyeLocalPosition = new Vector3(-0.35f, 1.1f, 0.6f);
                rearViewLocalPosition = new Vector3(0f, 1.2f, -0.6f);
                measured = true;
                return;
            }

            // Height is measured up from the body's floor rather than out from its centre:
            // "seven tenths of the way up the car" is a statement about the car, and it
            // holds whether the body is tall or low.
            float eyeY = local.min.y + local.size.y * eyeHeightFraction;
            float rearY = local.min.y + local.size.y * rearHeightFraction;

            driverEyeLocalPosition = new Vector3(
                local.center.x + local.extents.x * seatSideFraction,
                eyeY,
                local.center.z + local.extents.z * seatLengthFraction);

            // Centred, because a reversing view that is off to one side reads as the car
            // being crooked when it is not.
            rearViewLocalPosition = new Vector3(
                local.center.x,
                rearY,
                local.center.z + local.extents.z * rearLengthFraction);

            measured = true;
        }

#if UNITY_EDITOR
        // Lets the view-capture tool sweep seat positions without a rebuild per value.
        public void EditorSetFractions(float side, float length, float height, float rearLength, float rearHeight)
        {
            seatSideFraction = side;
            seatLengthFraction = length;
            eyeHeightFraction = height;
            rearLengthFraction = rearLength;
            rearHeightFraction = rearHeight;
            measured = false;
        }
#endif

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
