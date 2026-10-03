using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarParkingGame.Traffic
{
    // Alternates two sets of lights at a junction: one axis goes green while the other
    // holds red, with a shared yellow in between. Deliberately not a general-purpose
    // intersection simulation - two phases cover every junction this game needs.
    public class TrafficLightGroup : MonoBehaviour
    {
        [Serializable]
        public class Phase
        {
            public string name = "Phase";
            public List<TrafficLight> lights = new List<TrafficLight>();
        }

        [SerializeField] private List<Phase> phases = new List<Phase>();
        [SerializeField] private float greenSeconds = 8f;
        [SerializeField] private float yellowSeconds = 2f;

        private int activePhase;
        private float timer;
        private bool inYellow;

        private void Start()
        {
            if (phases.Count == 0)
            {
                Debug.LogWarning($"[TrafficLightGroup] '{name}' has no phases configured.", this);
                enabled = false;
                return;
            }

            ApplyPhase(TrafficLightState.Green);
        }

        private void Update()
        {
            timer += Time.deltaTime;

            if (!inYellow)
            {
                if (timer < greenSeconds)
                {
                    return;
                }

                timer = 0f;
                inYellow = true;
                ApplyPhase(TrafficLightState.Yellow);
                return;
            }

            if (timer < yellowSeconds)
            {
                return;
            }

            timer = 0f;
            inYellow = false;
            activePhase = (activePhase + 1) % phases.Count;
            ApplyPhase(TrafficLightState.Green);
        }

        private void ApplyPhase(TrafficLightState activeState)
        {
            for (int i = 0; i < phases.Count; i++)
            {
                TrafficLightState state = i == activePhase ? activeState : TrafficLightState.Red;

                foreach (TrafficLight light in phases[i].lights)
                {
                    light?.SetState(state);
                }
            }
        }
    }
}
