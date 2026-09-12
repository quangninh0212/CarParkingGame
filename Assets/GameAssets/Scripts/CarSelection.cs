using UnityEngine;
using System.Collections.Generic;

public class CarSelection : MonoBehaviour
{
    public GameObject allCarsContainer;

    private GameObject[] allCars;
    private int currentIndex = 0;

    void Start()
    {
        allCars = new GameObject[allCarsContainer.transform.childCount];

        for (int i = 0; i < allCarsContainer.transform.childCount; i++)
        {
            allCars[i] = allCarsContainer.transform.GetChild(i).gameObject;
            allCars[i].SetActive(false);
        }

        ApplySavedSelection();
    }

    void ShowCurrentCar()
    {
        foreach (GameObject car in allCars)
        {
            car.SetActive(false);
        }

        allCars[currentIndex].SetActive(true);
    }

    public void ApplySavedSelection()
    {
        if (allCars == null || allCars.Length == 0)
            return;

        if (PlayerPrefs.HasKey("SelectedCarIndex"))
        {
            currentIndex = Mathf.Clamp(
                PlayerPrefs.GetInt("SelectedCarIndex"),
                0,
                allCars.Length - 1
            );
        }

        ShowCurrentCar();
    }

    public void NextCar()
    {
        currentIndex = (currentIndex + 1) % allCars.Length;

        ShowCurrentCar();
    }

    public void PreviousCar()
    {
        currentIndex = (currentIndex - 1 + allCars.Length) % allCars.Length;

        ShowCurrentCar();
    }

    public void OnDoneButton()
    {
        PlayerPrefs.SetInt("SelectedCarIndex", currentIndex);
        PlayerPrefs.Save();

        // The showroom and the playable cars are two separate containers, each
        // with its own CarSelection. Push the new pick to the other one so the
        // car you drive matches the car you just picked, without a restart.
        var selections = FindObjectsByType<CarSelection>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (var selection in selections)
        {
            if (selection != this)
            {
                selection.ApplySavedSelection();
            }
        }

        FindFirstObjectByType<MainMenuManager>()?.BackToMainMenu();
    }
}
