using System.Collections.Generic;
using UnityEngine;

namespace CarParkingGame.Traffic
{
    // A route shared by traffic and pedestrians. Branching is expressed as a list of
    // continuations picked at random when the end is reached, which is enough to keep
    // agents circulating without any pathfinding at runtime.
    public class WaypointPath : MonoBehaviour
    {
        [SerializeField] private List<Transform> waypoints = new List<Transform>();
        [SerializeField] private bool loop = true;
        [SerializeField] private List<WaypointPath> continuations = new List<WaypointPath>();
        [SerializeField] private Color gizmoColor = new Color(0.3f, 0.8f, 1f, 0.9f);

        public int Count => waypoints.Count;
        public bool Loop => loop;
        public bool IsUsable => waypoints.Count >= 2;

        public Transform At(int index)
        {
            return index >= 0 && index < waypoints.Count ? waypoints[index] : null;
        }

        // Returns the next index on this path, or -1 when the path has ended and the
        // caller should move on to a continuation.
        public int NextIndex(int currentIndex)
        {
            int next = currentIndex + 1;

            if (next < waypoints.Count)
            {
                return next;
            }

            return loop ? 0 : -1;
        }

        public WaypointPath PickContinuation()
        {
            if (continuations == null || continuations.Count == 0)
            {
                return null;
            }

            for (int attempt = 0; attempt < continuations.Count; attempt++)
            {
                WaypointPath candidate = continuations[Random.Range(0, continuations.Count)];

                if (candidate != null && candidate.IsUsable)
                {
                    return candidate;
                }
            }

            return null;
        }

        public TrafficWaypoint WaypointDataAt(int index)
        {
            Transform point = At(index);
            return point != null ? point.GetComponent<TrafficWaypoint>() : null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;

            for (int i = 0; i < waypoints.Count; i++)
            {
                Transform current = waypoints[i];

                if (current == null)
                {
                    continue;
                }

                Gizmos.DrawSphere(current.position, 0.3f);

                Transform next = i + 1 < waypoints.Count
                    ? waypoints[i + 1]
                    : (loop && waypoints.Count > 1 ? waypoints[0] : null);

                if (next != null)
                {
                    Gizmos.DrawLine(current.position, next.position);
                }
            }
        }
    }
}
