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

        if (PlayerPrefs.HasKey("SelectedCarIndex"))
        {
            currentIndex = PlayerPrefs.GetInt("SelectedCarIndex");
        }

        ShowCurrentCar();
    }

    void ShowCurrentCar()
    {
        foreach (GameObject car in allCars)
        {
            car.SetActive(false);
        }

        allCars[currentIndex].SetActive(true);
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

        FindFirstObjectByType<MainMenuManager>()?.BackToMainMenu();
    }
}