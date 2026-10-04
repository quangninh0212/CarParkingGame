using System;
using System.IO;
using CarParkingGame.Progression;
using UnityEngine;

namespace CarParkingGame.Core
{
    // JSON progression store under Application.persistentDataPath.
    //
    // Not yet consumed by gameplay: GameManager/CarSelection still own their
    // PlayerPrefs keys until the mission-data phase moves progression over in one
    // coherent switch. Running both writers at once would let the two diverge on
    // real player data, so the handover is deliberately a single later step.
    public static class SaveManager
    {
        private const string SaveFileName = "carparking_save.json";

        private static SaveData data;

        public static event Action Loaded;
        public static event Action Saved;

        public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        public static bool IsLoaded => data != null;

        public static bool SaveFileExists => File.Exists(SavePath);

        public static SaveData Data
        {
            get
            {
                if (data == null)
                {
                    Load();
                }

                return data;
            }
        }

        public static SaveData Load()
        {
            SaveData loaded = ReadFromDisk();

            if (loaded == null)
            {
                data = CreateInitialSave();
                Save();
            }
            else
            {
                data = loaded;
            }

            // Not in Sanitize: that is a repair function, and handing out money is not a
            // repair. It belongs at the one point where a file becomes the save being
            // played, which happens once per run - so the demo starts every session with
            // money, and spending it inside a session still costs what it says.
            if (TestFunds.TryTopUp(data))
            {
                Debug.Log($"[SaveManager] Test funds: topped up to {data.coins} coins for the demo. Turn TestFunds.Enabled off before release.");
                Save();
            }

            Loaded?.Invoke();
            return data;
        }

        public static void Save()
        {
            if (data == null)
            {
                Debug.LogWarning("[SaveManager] Save() called before any data existed; nothing was written.");
                return;
            }

            data.Sanitize();
            string path = SavePath;

            try
            {
                string json = JsonUtility.ToJson(data, true);
                string directory = Path.GetDirectoryName(path);

                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                WriteAtomic(path, json);
                Saved?.Invoke();
            }
            catch (Exception error)
            {
                Debug.LogError($"[SaveManager] Failed to write save file at '{path}': {error}");
            }
        }

        // Wipes progression back to defaults. Legacy PlayerPrefs keys are left alone
        // so this stays reversible if the reset was a mistake.
        public static void ResetProgress(bool keepSettings = true)
        {
            SettingsSaveData preservedSettings = keepSettings && data != null ? data.settings : null;

            data = SaveData.CreateDefault();

            if (preservedSettings != null)
            {
                data.settings = preservedSettings;
            }

            Save();
            Loaded?.Invoke();
        }

        private static SaveData CreateInitialSave()
        {
            SaveData fresh = SaveData.CreateDefault();

            if (LegacyPlayerPrefsMigration.HasLegacyData())
            {
                LegacyPlayerPrefsMigration.Apply(fresh, LegacyPlayerPrefsMigration.Read());
                Debug.Log($"[SaveManager] Migrated legacy PlayerPrefs progression into '{SavePath}'.");
            }

            return fresh;
        }

        private static SaveData ReadFromDisk()
        {
            string path = SavePath;

            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                SaveData loaded = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));

                if (loaded == null || loaded.saveVersion <= 0)
                {
                    throw new InvalidDataException("file did not deserialize into usable save data");
                }

                if (loaded.saveVersion > SaveData.CurrentVersion)
                {
                    Debug.LogWarning($"[SaveManager] Save file version {loaded.saveVersion} is newer than this build's {SaveData.CurrentVersion}; loading it as-is rather than discarding it.");
                }

                loaded.Sanitize();
                return loaded;
            }
            catch (Exception error)
            {
                QuarantineCorruptSave(path, error);
                return null;
            }
        }

        private static void WriteAtomic(string path, string json)
        {
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, json);

            if (!File.Exists(path))
            {
                File.Move(tempPath, path);
                return;
            }

            try
            {
                File.Replace(tempPath, path, null);
            }
            catch (IOException)
            {
                // Some Android storage backends reject File.Replace; plain swap instead.
                File.Delete(path);
                File.Move(tempPath, path);
            }
        }

        private static void QuarantineCorruptSave(string path, Exception error)
        {
            string quarantinePath = $"{path}.corrupt_{DateTime.Now:yyyyMMdd_HHmmss}";

            try
            {
                File.Move(path, quarantinePath);
                Debug.LogError($"[SaveManager] Save file at '{path}' was unreadable ({error.Message}). It was kept at '{quarantinePath}' and defaults will be used.");
            }
            catch (Exception moveError)
            {
                Debug.LogError($"[SaveManager] Save file at '{path}' was unreadable ({error.Message}) and could not be set aside ({moveError.Message}). Defaults will be used without overwriting it until the next save.");
            }
        }
    }
}
