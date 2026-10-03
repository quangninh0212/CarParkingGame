using CarParkingGame.Core;
using UnityEngine;

namespace CarParkingGame.UI
{
    // Shows or hides objects depending on whether the game is in the menu or being played.
    //
    // Replaces the old Time.timeScale test, which could not tell "paused" from "showing a
    // result" from "sitting in the menu" - all three stop the clock - and so left the HUD
    // and the menu drawn over each other.
    public class SessionVisibility : MonoBehaviour
    {
        [SerializeField] private GameObject[] targets;

        [Tooltip("On, the targets are shown in the menu instead of during play.")]
        [SerializeField] private bool visibleInMenu;

        private GameSession session;

        private void OnEnable()
        {
            Subscribe();
            Apply();
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.ModeChanged -= OnModeChanged;
                session = null;
            }
        }

        // The session is a scene object like this one, so it may not have woken yet when
        // this enables; retrying from Update costs nothing and removes the ordering rule.
        private void Update()
        {
            if (session == null)
            {
                Subscribe();
                Apply();
            }
        }

        private void Subscribe()
        {
            if (session != null || GameSession.Instance == null)
            {
                return;
            }

            session = GameSession.Instance;
            session.ModeChanged += OnModeChanged;
        }

        private void OnModeChanged(GameplayMode mode)
        {
            Apply();
        }

        private void Apply()
        {
            if (targets == null)
            {
                return;
            }

            bool inMenu = session == null || session.InMenu;
            bool visible = visibleInMenu ? inMenu : !inMenu;

            foreach (GameObject target in targets)
            {
                if (target != null && target.activeSelf != visible)
                {
                    target.SetActive(visible);
                }
            }
        }
    }
}
