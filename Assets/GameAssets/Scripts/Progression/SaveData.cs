using System;
using System.Collections.Generic;
using CarParkingGame.Core;
using UnityEngine;

namespace CarParkingGame.Progression
{
    // Car ownership/colour, keyed by the car's index in the player-car container,
    // which is how the existing CarSelection already identifies cars. Phase 12
    // (Garage) introduces stable CarDefinition ids and bumps SaveData.CurrentVersion
    // to migrate these entries across; the index is what is verifiable today.
    [Serializable]
    public class CarSaveEntry
    {
        public const int DefaultColor = -1;

        public int carIndex;
        public bool owned;
        public int colorIndex = DefaultColor;
        public int engineLevel;
        public int brakeLevel;
        public int handlingLevel;

        public CarSaveEntry()
        {
        }

        public CarSaveEntry(int carIndex)
        {
            this.carIndex = carIndex;
        }
    }

    // Best result for one open-world challenge. Challenges are identified by a stable
    // string id from their definition asset, not by position in a list.
    [Serializable]
    public class ChallengeSaveEntry
    {
        public string challengeId = string.Empty;
        public bool completed;
        public int bestScore;
        public int bestStars;
    }

    [Serializable]
    public class SettingsSaveData
    {
        public int graphicsQuality = 1;
        public int targetFrameRate = 60;
        public float musicVolume = 0.8f;
        public float sfxVolume = 1f;
        public float steeringSensitivity = 1f;
    }

    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;
        public const int FirstMissionId = 1;
        public const int MinGraphicsQuality = 0;
        public const int MaxGraphicsQuality = 2;
        public const int MaxStars = 3;
        public const int MaxUpgradeLevel = 3;

        public int saveVersion = CurrentVersion;
        public bool migratedFromPlayerPrefs;
        public int selectedCarIndex;
        public int coins;
        public int currentMissionId = FirstMissionId;

        // Which round of test money this save has had. See TestFunds.
        public int testGrant;
        public GameMode lastGameMode = GameMode.MainMenu;
        public List<CarSaveEntry> cars = new List<CarSaveEntry>();
        public List<MissionProgressData> missions = new List<MissionProgressData>();
        public List<ChallengeSaveEntry> challenges = new List<ChallengeSaveEntry>();
        public SettingsSaveData settings = new SettingsSaveData();

        public static SaveData CreateDefault()
        {
            var data = new SaveData();
            data.GetOrCreateMission(FirstMissionId).unlocked = true;
            data.GetOrCreateCar(0).owned = true;
            return data;
        }

        public MissionProgressData FindMission(int missionId)
        {
            for (int i = 0; i < missions.Count; i++)
            {
                if (missions[i] != null && missions[i].missionId == missionId)
                {
                    return missions[i];
                }
            }

            return null;
        }

        public MissionProgressData GetOrCreateMission(int missionId)
        {
            MissionProgressData mission = FindMission(missionId);

            if (mission == null)
            {
                mission = new MissionProgressData(missionId);
                missions.Add(mission);
            }

            return mission;
        }

        public CarSaveEntry FindCar(int carIndex)
        {
            for (int i = 0; i < cars.Count; i++)
            {
                if (cars[i] != null && cars[i].carIndex == carIndex)
                {
                    return cars[i];
                }
            }

            return null;
        }

        public CarSaveEntry GetOrCreateCar(int carIndex)
        {
            CarSaveEntry car = FindCar(carIndex);

            if (car == null)
            {
                car = new CarSaveEntry(carIndex);
                cars.Add(car);
            }

            return car;
        }

        public ChallengeSaveEntry FindChallenge(string challengeId)
        {
            if (string.IsNullOrEmpty(challengeId))
            {
                return null;
            }

            for (int i = 0; i < challenges.Count; i++)
            {
                if (challenges[i] != null && challenges[i].challengeId == challengeId)
                {
                    return challenges[i];
                }
            }

            return null;
        }

        public ChallengeSaveEntry GetOrCreateChallenge(string challengeId)
        {
            ChallengeSaveEntry challenge = FindChallenge(challengeId);

            if (challenge == null)
            {
                challenge = new ChallengeSaveEntry { challengeId = challengeId };
                challenges.Add(challenge);
            }

            return challenge;
        }

        public bool IsMissionUnlocked(int missionId)
        {
            if (MissionUnlocking.EveryMissionOpen || missionId == FirstMissionId)
            {
                return true;
            }

            MissionProgressData mission = FindMission(missionId);
            return mission != null && mission.unlocked;
        }

        public bool IsMissionCompleted(int missionId)
        {
            MissionProgressData mission = FindMission(missionId);
            return mission != null && mission.completed;
        }

        // Repairs partially-written or hand-edited save files so a damaged-but-readable
        // file degrades into something playable instead of throwing at an arbitrary
        // point later. Anything unrecoverable is handled by SaveManager's quarantine.
        public void Sanitize()
        {
            if (saveVersion <= 0)
            {
                saveVersion = CurrentVersion;
            }

            settings ??= new SettingsSaveData();
            cars ??= new List<CarSaveEntry>();
            missions ??= new List<MissionProgressData>();
            challenges ??= new List<ChallengeSaveEntry>();

            settings.graphicsQuality = Mathf.Clamp(settings.graphicsQuality, MinGraphicsQuality, MaxGraphicsQuality);
            settings.targetFrameRate = settings.targetFrameRate <= 45 ? 30 : 60;
            settings.musicVolume = Mathf.Clamp01(settings.musicVolume);
            settings.sfxVolume = Mathf.Clamp01(settings.sfxVolume);
            settings.steeringSensitivity = Mathf.Clamp(settings.steeringSensitivity, 0.25f, 3f);

            coins = Mathf.Max(0, coins);
            selectedCarIndex = Mathf.Max(0, selectedCarIndex);
            currentMissionId = Mathf.Max(FirstMissionId, currentMissionId);

            RemoveNullAndDuplicateEntries();

            for (int i = 0; i < missions.Count; i++)
            {
                MissionProgressData mission = missions[i];
                mission.missionId = Mathf.Max(FirstMissionId, mission.missionId);
                mission.bestScore = Mathf.Max(0, mission.bestScore);
                mission.bestStars = Mathf.Clamp(mission.bestStars, 0, MaxStars);

                if (mission.completed)
                {
                    mission.unlocked = true;
                }
            }

            for (int i = 0; i < cars.Count; i++)
            {
                CarSaveEntry car = cars[i];
                car.engineLevel = Mathf.Clamp(car.engineLevel, 0, MaxUpgradeLevel);
                car.brakeLevel = Mathf.Clamp(car.brakeLevel, 0, MaxUpgradeLevel);
                car.handlingLevel = Mathf.Clamp(car.handlingLevel, 0, MaxUpgradeLevel);
            }

            for (int i = 0; i < challenges.Count; i++)
            {
                ChallengeSaveEntry challenge = challenges[i];
                challenge.bestScore = Mathf.Max(0, challenge.bestScore);
                challenge.bestStars = Mathf.Clamp(challenge.bestStars, 0, MaxStars);
            }

            GetOrCreateMission(FirstMissionId).unlocked = true;
            GetOrCreateCar(0).owned = true;
        }

        private void RemoveNullAndDuplicateEntries()
        {
            var seenMissions = new HashSet<int>();

            for (int i = missions.Count - 1; i >= 0; i--)
            {
                if (missions[i] == null || !seenMissions.Add(missions[i].missionId))
                {
                    missions.RemoveAt(i);
                }
            }

            var seenCars = new HashSet<int>();

            for (int i = cars.Count - 1; i >= 0; i--)
            {
                if (cars[i] == null || cars[i].carIndex < 0 || !seenCars.Add(cars[i].carIndex))
                {
                    cars.RemoveAt(i);
                }
            }

            var seenChallenges = new HashSet<string>();

            for (int i = challenges.Count - 1; i >= 0; i--)
            {
                if (challenges[i] == null
                    || string.IsNullOrEmpty(challenges[i].challengeId)
                    || !seenChallenges.Add(challenges[i].challengeId))
                {
                    challenges.RemoveAt(i);
                }
            }
        }
    }
}
