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
            // A head with no amber lamp shows red through the amber phase. That is what the
            // phase means to a driver - stop if you can - and a two lamp head that went
            // dark for the whole of it would read as a broken light rather than a changing
            // one.
            bool amber = state == TrafficLightState.Yellow;
            bool hasAmberLamp = yellowGlow != null;

            SetGlow(greenGlow, state == TrafficLightState.Green);
            SetGlow(yellowGlow, amber);
            SetGlow(redGlow, state == TrafficLightState.Red || (amber && !hasAmberLamp));
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
