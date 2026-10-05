using CarParkingGame.Parking;
using UnityEngine;

namespace CarParkingGame.Missions
{
    // Fireworks over a bay the moment it is filled, and a bigger set over the car when the
    // level is finished.
    //
    // Both, rather than only the second: a level that asks for four bays should say so
    // four times, and the three in the middle are the ones that otherwise pass with
    // nothing but a number on the HUD changing.
    public class MissionCelebration : MonoBehaviour
    {
        [SerializeField] private MissionManager missions;
        [SerializeField] private GameObject bayEffect;
        [SerializeField] private GameObject finishEffect;

        [Tooltip("How high above the bay the burst goes off.")]
        [SerializeField] private float height = 3.2f;

        [SerializeField] private float effectSeconds = 5f;

        private MissionManager Missions => missions != null ? missions : MissionManager.Instance;

        private void OnEnable()
        {
            MissionManager runner = Missions;

            if (runner == null)
            {
                return;
            }

            runner.BayProgressChanged += OnBayFilled;
            runner.MissionEnded += OnMissionEnded;
        }

        private void OnDisable()
        {
            MissionManager runner = Missions;

            if (runner == null)
            {
                return;
            }

            runner.BayProgressChanged -= OnBayFilled;
            runner.MissionEnded -= OnMissionEnded;
        }

        private void OnBayFilled(int filled, int wanted)
        {
            // Also raised when a mission starts, with nothing filled yet.
            if (filled <= 0)
            {
                return;
            }

            // The last bay is the end of the level, and that gets its own, larger burst a
            // moment later. Two sets of fireworks on one spot is a mess.
            if (filled >= wanted)
            {
                return;
            }

            Burst(bayEffect, BayPosition(filled - 1));
        }

        private void OnMissionEnded(MissionResult result)
        {
            if (result == null || !result.success)
            {
                return;
            }

            MissionManager runner = Missions;
            int last = runner != null ? Mathf.Max(0, runner.BaysFilled - 1) : 0;

            Burst(finishEffect, BayPosition(last));
        }

        // Over the bay that was just filled, falling back to the active one. Read from the
        // mission rather than from the car: the car is about to be put somewhere else, and
        // the fireworks belong where the player parked.
        private Vector3 BayPosition(int index)
        {
            MissionManager runner = Missions;
            MissionAuthoring mission = runner != null ? runner.ActiveAuthoring : null;

            ParkingZone zone = mission != null ? mission.GetBay(index) : runner?.ActiveZone;

            if (zone == null)
            {
                return transform.position;
            }

            return zone.WorldCenter + Vector3.up * height;
        }

        private void Burst(GameObject effect, Vector3 at)
        {
            if (effect == null)
            {
                return;
            }

            GameObject spawned = Instantiate(effect, at, Quaternion.identity);
            Destroy(spawned, effectSeconds);
        }
    }
}
