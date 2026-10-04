using UnityEngine;

namespace CarParkingGame.Parking
{
    public enum ParkingType
    {
        Forward,
        Reverse,
        Parallel
    }

    // A parking bay as an oriented box. Containment is measured against the
    // vehicle's own footprint corners instead of a single trigger touch, so a car
    // hanging halfway out of the bay does not count as parked.
    //
    // Centre and size are in metres in the object's rotation frame and deliberately
    // ignore its scale. The legacy triggers this component sits on are thin plates
    // scaled to (1, 2.3, 0.19); measuring through that scale shrank a 5 m bay to under
    // 1 m in the world, which no car could ever fit.
    public class ParkingZone : MonoBehaviour
    {
        [SerializeField] private Vector3 center = Vector3.zero;
        [SerializeField] private Vector3 size = new Vector3(3f, 2.5f, 6f);

        [Tooltip("On when a correctly parked car faces opposite to this object's forward axis " +
                 "(a nose-in bay whose end marker faces out of the bay).")]
        [SerializeField] private bool parkedFacingBackward;

        [Tooltip("Off for a bay that takes the car either way round. The bay is then painted without an arrow, which is how the player is told.")]
        [SerializeField] private bool requireHeading = true;

        public Vector3 Size => size;
        public Vector3 WorldCenter => transform.position + transform.rotation * center;
        public Vector3 ParkedHeading => parkedFacingBackward ? -transform.forward : transform.forward;
        public bool RequireHeading => requireHeading;

        // Height is deliberately ignored: bays sit on kerbs, ramps and garage floors
        // at slightly different heights, and a vertical test only adds false negatives.
        public bool ContainsPoint(Vector3 worldPoint)
        {
            Vector3 local = Quaternion.Inverse(transform.rotation) * (worldPoint - transform.position) - center;

            return Mathf.Abs(local.x) <= size.x * 0.5f
                && Mathf.Abs(local.z) <= size.z * 0.5f;
        }

        public float GetContainment(Transform vehicle, Vector2 halfExtents)
        {
            if (vehicle == null)
            {
                return 0f;
            }

            var corners = new Vector3[4]
            {
                new Vector3(-halfExtents.x, 0f, halfExtents.y),
                new Vector3(halfExtents.x, 0f, halfExtents.y),
                new Vector3(halfExtents.x, 0f, -halfExtents.y),
                new Vector3(-halfExtents.x, 0f, -halfExtents.y)
            };

            int inside = 0;

            for (int i = 0; i < corners.Length; i++)
            {
                if (ContainsPoint(vehicle.TransformPoint(corners[i])))
                {
                    inside++;
                }
            }

            return inside / (float)corners.Length;
        }

        // Smallest angle between the vehicle's heading and the bay's axis. With
        // allowOpposite, a car parked facing the other way still counts as aligned.
        public float GetHeadingError(Transform vehicle, bool allowOpposite)
        {
            if (vehicle == null)
            {
                return 180f;
            }

            float angle = Vector3.Angle(Flatten(vehicle.forward), Flatten(ParkedHeading));

            return allowOpposite ? Mathf.Min(angle, 180f - angle) : angle;
        }

#if UNITY_EDITOR
        // Lets the setup tools place the bay without exposing runtime setters.
        public void EditorSetBox(Vector3 boxCenter, Vector3 boxSize)
        {
            center = boxCenter;
            size = boxSize;
        }

        public void EditorSetRequireHeading(bool required)
        {
            requireHeading = required;
        }

        public void EditorSetParkedFacingBackward(bool facingBackward)
        {
            parkedFacingBackward = facingBackward;
        }
#endif

        private static Vector3 Flatten(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude < 0.000001f ? Vector3.forward : direction.normalized;
        }

        private void OnDrawGizmos()
        {
            // Scale is left out on purpose, matching how containment is measured.
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            Gizmos.DrawCube(center, new Vector3(size.x, 0.05f, size.z));

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube(center, size);

            // Arrow showing the heading a parked car should end up facing.
            float sign = parkedFacingBackward ? -1f : 1f;
            Vector3 tip = center + Vector3.forward * (sign * size.z * 0.5f);
            Gizmos.DrawLine(center, tip);
            Gizmos.DrawLine(tip, tip + new Vector3(0.3f, 0f, -0.4f * sign));
            Gizmos.DrawLine(tip, tip + new Vector3(-0.3f, 0f, -0.4f * sign));

            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
