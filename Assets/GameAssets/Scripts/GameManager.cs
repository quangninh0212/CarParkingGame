using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager instance { get; private set; }

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
        if (instance == null)
        {
            instance = this;
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
}