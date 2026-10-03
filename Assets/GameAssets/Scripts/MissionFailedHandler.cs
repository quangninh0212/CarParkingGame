using CarParkingGame.Core;
using UnityEngine;
using UnityEngine.UI;

// Superseded by the scoring system, which charges points for a collision instead of
// ending the run on contact.
//
// This is the bug that made missions unfinishable: the track's barriers are tagged
// "Cone", so clipping one while lining the car up froze the car through
// SetVehicleEnabled(false) and left the player unable to finish the park. Wherever a
// GameSession exists this component stands down, and cones cost score instead.
public class MissionFailedHandler : MonoBehaviour
{
    public GameObject missionFailedUI;
    public Button retryButton;
    public CarController[] carControllers;

    void Start()
    {
        if (GameSession.Exists)
        {
            enabled = false;
            return;
        }

        if (missionFailedUI != null)
        {
            missionFailedUI.SetActive(false);
        }

        if (retryButton != null)
        {
            retryButton.onClick.AddListener(RetryMission);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (GameSession.Exists)
        {
            return;
        }

        if (collision.gameObject.CompareTag("Cone"))
        {
            if (missionFailedUI != null)
            {
                missionFailedUI.SetActive(true);
            }

            foreach (var car in carControllers)
            {
                if (car != null)
                {
                    car.SetVehicleEnabled(false);
                }
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void RetryMission()
    {
        foreach (var car in carControllers)
        {
            if (car != null)
            {
                car.SetVehicleEnabled(true);
            }
        }

        GameManager.Instance.SpawnPlayerAtMissionStart();

        if (missionFailedUI != null)
        {
            missionFailedUI.SetActive(false);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
