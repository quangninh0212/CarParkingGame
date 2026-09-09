using UnityEngine;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    public GameObject mainMenu;
    public GameObject selectCarUI;
    public GameObject selectMissionUI;
    public GameObject missionInformation;

    public MonoBehaviour[] carControllerScripts;
    public Button[] missionButtons;

    public GameObject menuCamera;
    public GameObject mainCamera;

    private bool isGamePaused = false;

    public Button resumeButton;

    public GameObject mobileControls;

    void Start()
    {
        ShowMainMenu();

        InitializeMissionButtons();

        UpdateMissionButtons();

        resumeButton.gameObject.SetActive(false);
    }

    public void OpenSelectMissionUI()
    {
        mainMenu.SetActive(false);
        selectCarUI.SetActive(false);
        selectMissionUI.SetActive(true);
    }

    public void OpenSelectCarUI()
    {
        mainMenu.SetActive(false);
        selectCarUI.SetActive(true);
        selectMissionUI.SetActive(false);
    }

    public void BackToMainMenu()
    {
        mainMenu.SetActive(true);
        selectCarUI.SetActive(false);
        selectMissionUI.SetActive(false);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void ShowMainMenu()
    {
        isGamePaused = true;
        Time.timeScale = 0f;

        mainMenu.SetActive(true);
        selectCarUI.SetActive(false);
        selectMissionUI.SetActive(false);
        mobileControls.SetActive(false);

        if (menuCamera != null)
            menuCamera.SetActive(true);

        if (mainCamera != null)
            mainCamera.gameObject.SetActive(false);

        if (missionInformation != null)
            missionInformation.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        resumeButton.gameObject.SetActive(
            isGamePaused && Time.timeSinceLevelLoad > 0
        );
    }

    public void ResumeGame()
    {
        isGamePaused = false;
        Time.timeScale = 1f;

        mainMenu.SetActive(false);
        selectCarUI.SetActive(false);
        selectMissionUI.SetActive(false);
        mobileControls.SetActive(true);

        if (menuCamera != null)
            menuCamera.SetActive(false);

        if (mainCamera != null)
            mainCamera.gameObject.SetActive(true);

        if (missionInformation != null)
            missionInformation.SetActive(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void ReplayMission(int missionIndex)
    {
        GameManager.Instance.ReplayMission(missionIndex);
        ResumeGame();
    }

    public void UpdateMissionButtons()
    {
        for (int i = 0; i < missionButtons.Length; i++)
        {
            missionButtons[i].interactable =
                GameManager.Instance.missionCompleted[i]
                || i == GameManager.Instance.currentMission;
        }
    }

    void InitializeMissionButtons()
    {
        for (int i = 0; i < missionButtons.Length; i++)
        {
            int missionIndex = i;

            if (i < GameManager.Instance.missionCompleted.Length)
            {
                missionButtons[i].interactable =
                    GameManager.Instance.missionCompleted[i];

                missionButtons[i].onClick.AddListener(
                    () => ReplayMission(missionIndex)
                );
            }
        }
    }
}