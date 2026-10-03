using System;
using UnityEngine;

namespace CarParkingGame.Core
{
    public enum GameMode
    {
        None,
        MainMenu,
        Practice,
        OpenWorld
    }

    public enum GameState
    {
        Menu,
        Playing,
        Paused,
        Result
    }

    // Self-initializing (no scene wiring required) so later phases can adopt it
    // incrementally without editing complete_track_demo.unity. Not yet consumed by
    // any existing script; MainMenuManager/GameManager keep driving Time.timeScale
    // and panel visibility themselves until a later phase migrates them over.
    public class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        public event Action<GameMode> GameModeChanged;
        public event Action<GameState> GameStateChanged;

        public GameMode CurrentMode { get; private set; } = GameMode.None;
        public GameState CurrentState { get; private set; } = GameState.Menu;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureInstance()
        {
            if (Instance != null)
            {
                return;
            }

            var go = new GameObject(nameof(GameFlowManager));
            Instance = go.AddComponent<GameFlowManager>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetGameMode(GameMode mode)
        {
            if (CurrentMode == mode)
            {
                return;
            }

            CurrentMode = mode;
            GameModeChanged?.Invoke(CurrentMode);
        }

        public void SetGameState(GameState state)
        {
            if (CurrentState == state)
            {
                return;
            }

            CurrentState = state;
            GameStateChanged?.Invoke(CurrentState);
        }

        public void Pause()
        {
            Time.timeScale = 0f;
            SetGameState(GameState.Paused);
        }

        public void Resume()
        {
            Time.timeScale = 1f;
            SetGameState(GameState.Playing);
        }
    }
}
