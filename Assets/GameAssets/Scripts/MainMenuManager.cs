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
    public Camera mainCamera;

    private bool isGamePaused = false;

    public Button resumeButton;

    void Start()
    {
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
}