using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameObject missionInformation;
    public Text missionTextLabel;
    public Image[] missionImages;

    public Transform[] missionStartPoints;
    public GameObject[] playerCars;
    public GameObject[] missionAreas;
    public ParkingTrigger[] parkingTriggers;

    public int currentMission = 0;
    public bool[] missionCompleted;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        missionCompleted = new bool[missionStartPoints.Length];

        ShowMissionTextForCurrentMission();
        SetActiveMissionArea();
        SpawnPlayerAtMissionStart();
    }

    public void ReplayMission(int missionIndex)
    {
        if (missionIndex < 0 || missionIndex >= missionStartPoints.Length)
            return;

        currentMission = missionIndex;
        UpdateMissionText();
    }

    private void UpdateMissionText()
    {
        if (currentMission < parkingTriggers.Length &&
            parkingTriggers[currentMission].isReverseMission)
        {
            missionTextLabel.text = "Park Car in Reverse";
        }
        else
        {
            missionTextLabel.text = "Park Car Straight";
        }

        for (int i = 0; i < missionImages.Length; i++)
        {
            missionImages[i].gameObject.SetActive(i == currentMission);
        }
    }

    private IEnumerator DisplayMissionText()
    {
        missionInformation.SetActive(true);

        yield return new WaitForSeconds(2f);

        missionInformation.SetActive(false);
    }

    public void ShowMissionTextForCurrentMission()
    {
        UpdateMissionText();
        StartCoroutine(DisplayMissionText());
    }

    public void SetActiveMissionArea()
    {
        for (int i = 0; i < missionAreas.Length; i++)
        {
            missionAreas[i].SetActive(i == currentMission);
        }
    }

    public void SpawnPlayerAtMissionStart()
    {
        if (currentMission < missionStartPoints.Length && playerCars.Length > 0)
        {
            foreach (var car in playerCars)
            {
                if (car != null)
                {
                    car.transform.position =
                        missionStartPoints[currentMission].position;

                    car.transform.rotation =
                        missionStartPoints[currentMission].rotation;
                }
            }
        }
    }
    public void CompleteMission()
    {
        missionCompleted[currentMission] = true;

        currentMission++;

        if (currentMission >= missionStartPoints.Length)
        {
            missionTextLabel.text = "All Missions Completed";
        }
        else
        {
            missionCompleted[currentMission] = true;
            UpdateMissionText();
        }
    }
}