using UnityEngine;
using UnityEngine.UI;

public class ParkingTrigger : MonoBehaviour
{
    public GameObject missionPassedUI;
    public Button nextMissionButton;
    public CarController[] carControllers;
    public bool isReverseMission = false;

    private bool missionCompleted = false;

    void Start()
    {
        missionPassedUI.SetActive(false);
        nextMissionButton.onClick.AddListener(NextMission);
    }

    void OnTriggerEnter(Collider other)
    {
        if (missionCompleted) return;

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
                car.maxAcceleration = 0;
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
                car.maxAcceleration = 5;
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