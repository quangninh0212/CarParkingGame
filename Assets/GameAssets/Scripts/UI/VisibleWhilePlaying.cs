using UnityEngine;

namespace CarParkingGame.UI
{
    // Shows or hides objects depending on whether the game is running or sitting in the
    // menu. Time.timeScale is the signal because that is already how MainMenuManager
    // pauses the game - no new state to keep in step with it.
    public class VisibleWhilePlaying : MonoBehaviour
    {
        [SerializeField] private GameObject[] targets;

        [Tooltip("When on, the targets are shown in the menu instead of during play.")]
        [SerializeField] private bool invert;

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            Apply();
        }

        private void Apply()
        {
            if (targets == null)
            {
                return;
            }

            bool playing = Time.timeScale > 0f;
            bool visible = invert ? !playing : playing;

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
