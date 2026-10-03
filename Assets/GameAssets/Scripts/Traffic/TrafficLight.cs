using UnityEngine;

namespace CarParkingGame.Traffic
{
    public enum TrafficLightState
    {
        Green,
        Yellow,
        Red
    }

    // A single light head. Its state is driven by a TrafficLightGroup rather than its own
    // timer, so lights at one junction cannot drift out of step with each other.
    public class TrafficLight : MonoBehaviour
    {
        [SerializeField] private GameObject greenGlow;
        [SerializeField] private GameObject yellowGlow;
        [SerializeField] private GameObject redGlow;

        private TrafficLightState state = TrafficLightState.Red;

        public TrafficLightState State => state;

        // Yellow counts as "stop if you can": traffic that has not entered yet holds.
        public bool RequiresStop => state != TrafficLightState.Green;

        private void Awake()
        {
            ApplyState();
        }

        public void SetState(TrafficLightState next)
        {
            if (state == next)
            {
                return;
            }

            state = next;
            ApplyState();
        }

        private void ApplyState()
        {
            SetGlow(greenGlow, state == TrafficLightState.Green);
            SetGlow(yellowGlow, state == TrafficLightState.Yellow);
            SetGlow(redGlow, state == TrafficLightState.Red);
        }

        private static void SetGlow(GameObject glow, bool on)
        {
            if (glow != null)
            {
                glow.SetActive(on);
            }
        }
    }
}
