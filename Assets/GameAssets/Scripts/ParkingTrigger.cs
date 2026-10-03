using CarParkingGame.Core;
using UnityEngine;
using UnityEngine.UI;

// Superseded by MissionManager + ParkingValidator. It stays in the project only so that
// scenes which still carry it keep working; wherever a GameSession exists it stands down
// completely, because "touching this trigger ends the mission" bypasses the real parking
// check and freezes the car.
public class ParkingTrigger : MonoBehaviour
{
    public GameObject missionPassedUI;
    public Button nextMissionButton;
    public CarController[] carControllers;
    public bool isReverseMission = false;

    private bool missionCompleted = false;

    void Start()
    {
        if (GameSession.Exists)
        {
            enabled = false;
            return;
        }

        if (missionPassedUI != null)
        {
            missionPassedUI.SetActive(false);
        }

        if (nextMissionButton != null)
        {
            nextMissionButton.onClick.AddListener(NextMission);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (missionCompleted || GameSession.Exists) return;

        if (isReverseMission)
        {
            if (other.CompareTag("Reverse"))
            {
                CompleteMission();
            }
        }
        else
        {
            if (other.CompareTag("Player"))
            {
                CompleteMission();
            }
        }
    }

    void CompleteMission()
    {
        missionCompleted = true;
        ShowMissionPassed();

        GameManager.Instance.CompleteMission();
    }

    void ShowMissionPassed()
    {
        missionPassedUI.SetActive(true);

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

    public void NextMission()
    {
        foreach (var car in carControllers)
        {
            if (car != null)
            {
                car.SetVehicleEnabled(true);
            }
        }

        GameManager.Instance.SpawnPlayerAtMissionStart();

        missionPassedUI.SetActive(false);

        GameManager.Instance.SetActiveMissionArea();

        GameManager.Instance.ShowMissionTextForCurrentMission();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        GameManager.Instance.SaveProgress();
    }
}