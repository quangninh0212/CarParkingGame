using UnityEngine;
using UnityEngine.UI;

namespace CarParkingGame.UI
{
    // Shows or hides one panel when its button is pressed. Exists so generated UI can wire
    // panel toggles without a serialized UnityEvent target per button.
    [RequireComponent(typeof(Button))]
    public class PanelToggleButton : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private bool show = true;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(Apply);
        }

        private void Apply()
        {
            if (panel != null)
            {
                panel.SetActive(show);
            }
        }
    }
}
