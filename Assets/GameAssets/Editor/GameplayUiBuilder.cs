using CarParkingGame.Garage;
using CarParkingGame.Missions;
using CarParkingGame.Settings;
using CarParkingGame.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CarParkingGame.EditorTools
{
    // Builds a working (deliberately plain) UI for the new systems, so the mission
    // pipeline, HUD and result screens can be used by a person instead of only existing
    // as code.
    //
    // This is functional, not pretty: positions, sizes and colours are reasonable defaults
    // chosen without being able to see the result. Visual polish is a job for the Editor.
    // Everything is created under one canvas named "NewGameplayUI" so it can be deleted in
    // one go without touching the legacy UI.
    public static class GameplayUiBuilder
    {
        private const string ScenePath = "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity";
        private const string CanvasName = "NewGameplayUI";
        private const string MissionCatalogPath = "Assets/GameAssets/ScriptableObjects/MissionCatalog.asset";

        private static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.12f, 0.92f);
        private static readonly Color AccentColor = new Color(0.2f, 0.6f, 1f, 1f);
        private static readonly Color ButtonColor = new Color(0.18f, 0.2f, 0.26f, 1f);

        [MenuItem("Tools/Car Parking/Build New Gameplay UI")]
        public static void BuildInOpenScene()
        {
            Build();
            Debug.Log("[GameplayUiBuilder] Done. Save the scene to keep it.");
        }

        public static void BuildFromCommandLine()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError($"[GameplayUiBuilder] Could not open '{ScenePath}'.");
                EditorApplication.Exit(1);
                return;
            }

            Build();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[GameplayUiBuilder] Saved '{ScenePath}'.");
            EditorApplication.Exit(0);
        }

        private static void Build()
        {
            GameObject existing = GameObject.Find(CanvasName);

            if (existing != null)
            {
                Object.DestroyImmediate(existing);
                Debug.Log("[GameplayUiBuilder] Replaced the previous generated UI.");
            }

            GameObject canvasObject = CreateCanvas();

            GameObject hud = BuildHud(canvasObject.transform);
            GameObject missionList = BuildMissionSelection(canvasObject.transform);
            GameObject results = BuildResultScreens(canvasObject.transform);

            GameObject garage = BuildGaragePanel(canvasObject.transform);
            GameObject settings = BuildSettingsPanel(canvasObject.transform);

            GameObject openButton = BuildOpenButton(canvasObject.transform, missionList);
            GameObject garageButton = BuildMenuButton(canvasObject.transform, "OpenGarageButton", "GARAGE (NEW)", 140f, garage);
            GameObject settingsButton = BuildMenuButton(canvasObject.transform, "OpenSettingsButton", "SETTINGS (NEW)", 220f, settings);

            // The HUD belongs to play, the practice entry button belongs to the menu, so
            // neither sits on top of the other.
            var hudVisibility = canvasObject.AddComponent<VisibleWhilePlaying>();
            SetPrivateArray(hudVisibility, "targets", new Object[] { hud });

            var menuVisibility = canvasObject.AddComponent<VisibleWhilePlaying>();
            SetPrivateArray(menuVisibility, "targets", new Object[] { openButton, garageButton, settingsButton });
            SetPrivate(menuVisibility, "invert", true);

            Debug.Log($"[GameplayUiBuilder] Built '{CanvasName}' with a HUD, car control buttons, a mission list and result screens. " +
                      "It is a plain layout meant to be usable, not final art.");

            if (hud == null || missionList == null || results == null)
            {
                Debug.LogWarning("[GameplayUiBuilder] Some part of the UI did not build; check the log above.");
            }
        }

        private static GameObject CreateCanvas()
        {
            var canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create gameplay UI");

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Sorted above the legacy canvases so the new screens are not hidden behind them.
            canvas.sortingOrder = 100;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            // Landscape: matching height keeps HUD elements the same size on tall phones.
            scaler.matchWidthOrHeight = 1f;

            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));

                Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
                Debug.Log("[GameplayUiBuilder] Added an EventSystem (the scene had none).");
            }

            return canvasObject;
        }

        private static GameObject BuildHud(Transform parent)
        {
            GameObject hud = CreateChild(parent, "HUD");
            RectTransform rect = Stretch(hud);

            // Speed, top-right.
            GameObject speedPanel = CreatePanel(hud.transform, "SpeedPanel", new Vector2(1f, 1f), new Vector2(-150f, -90f), new Vector2(220f, 120f));
            Text speedLabel = CreateLabel(speedPanel.transform, "Speed", "0", 64, TextAnchor.MiddleCenter);
            SetAnchors(speedLabel.rectTransform, new Vector2(0.5f, 0.65f), new Vector2(0f, 0f), new Vector2(200f, 70f));
            Text unitLabel = CreateLabel(speedPanel.transform, "Unit", "km/h", 24, TextAnchor.MiddleCenter);
            SetAnchors(unitLabel.rectTransform, new Vector2(0.5f, 0.2f), new Vector2(0f, 0f), new Vector2(200f, 30f));

            var speedometer = hud.AddComponent<SpeedometerView>();
            SetPrivate(speedometer, "speedLabel", speedLabel);
            SetPrivate(speedometer, "unitLabel", unitLabel);

            // Mission name, score and coins, top-left.
            GameObject infoPanel = CreatePanel(hud.transform, "InfoPanel", new Vector2(0f, 1f), new Vector2(230f, -70f), new Vector2(420f, 100f));
            Text missionName = CreateLabel(infoPanel.transform, "MissionName", "-", 26, TextAnchor.UpperLeft);
            SetAnchors(missionName.rectTransform, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(390f, 34f));
            Text score = CreateLabel(infoPanel.transform, "Score", "100", 30, TextAnchor.UpperLeft);
            SetAnchors(score.rectTransform, new Vector2(0.25f, 0.25f), Vector2.zero, new Vector2(180f, 36f));
            Text coins = CreateLabel(infoPanel.transform, "Coins", "0", 30, TextAnchor.UpperRight);
            SetAnchors(coins.rectTransform, new Vector2(0.75f, 0.25f), Vector2.zero, new Vector2(180f, 36f));
            Text timer = CreateLabel(infoPanel.transform, "Timer", "0:00", 26, TextAnchor.MiddleCenter);
            SetAnchors(timer.rectTransform, new Vector2(0.5f, -0.45f), Vector2.zero, new Vector2(160f, 36f));

            // Parking feedback, bottom-centre.
            GameObject feedback = CreatePanel(hud.transform, "ParkingFeedback", new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(520f, 86f));
            Image indicator = CreateImage(feedback.transform, "StateIndicator", new Color(0.8f, 0.25f, 0.25f));
            SetAnchors(indicator.rectTransform, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(480f, 14f));
            Image progress = CreateImage(feedback.transform, "ProgressBar", AccentColor);

            // A Filled image with no sprite silently renders as a full quad, so the bar
            // would never move. Any sprite fixes it; the built-in UI sprite is always present.
            progress.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            progress.type = Image.Type.Filled;
            progress.fillMethod = Image.FillMethod.Horizontal;
            progress.fillAmount = 0f;
            SetAnchors(progress.rectTransform, new Vector2(0.5f, 0.42f), Vector2.zero, new Vector2(480f, 18f));
            Text hint = CreateLabel(feedback.transform, "Hint", string.Empty, 22, TextAnchor.MiddleCenter);
            SetAnchors(hint.rectTransform, new Vector2(0.5f, 0.12f), Vector2.zero, new Vector2(500f, 30f));

            BuildVehicleControlButtons(hud);

            var hudView = hud.AddComponent<GameplayHudView>();
            SetPrivate(hudView, "missionNameLabel", missionName);
            SetPrivate(hudView, "scoreLabel", score);
            SetPrivate(hudView, "coinLabel", coins);
            SetPrivate(hudView, "timerLabel", timer);
            SetPrivate(hudView, "timerContainer", timer.gameObject);
            SetPrivate(hudView, "parkingStateIndicator", indicator);
            SetPrivate(hudView, "parkingProgressBar", progress);
            SetPrivate(hudView, "parkingHintLabel", hint);
            SetPrivate(hudView, "missionManager", Object.FindFirstObjectByType<MissionManager>());

            return hud;
        }

        // A column of car controls on the right, above the thumb, clear of SimpleInput's
        // joystick on the left and the brake button.
        private static void BuildVehicleControlButtons(GameObject hud)
        {
            // Lowest button sits clear of the bottom edge and of rounded screen corners.
            Button headlight = CreateControlButton(hud.transform, "HeadlightButton", "LIGHT", 380f);
            Button horn = CreateControlButton(hud.transform, "HornButton", "HORN", 290f);
            Button left = CreateControlButton(hud.transform, "LeftIndicatorButton", "< L", 200f);
            Button right = CreateControlButton(hud.transform, "RightIndicatorButton", "R >", 110f);
            Button camera = CreateControlButton(hud.transform, "CameraButton", "CAM", 470f);

            var controls = hud.AddComponent<VehicleControlButtons>();
            SetPrivate(controls, "headlightButton", headlight);
            SetPrivate(controls, "hornButton", horn);
            SetPrivate(controls, "leftIndicatorButton", left);
            SetPrivate(controls, "rightIndicatorButton", right);
            SetPrivate(controls, "cameraButton", camera);
        }

        private static Button CreateControlButton(Transform parent, string name, string caption, float y)
        {
            Button button = CreateButton(parent, name, caption, new Vector2(1f, 0f), new Vector2(150f, 78f));
            SetAnchors(button.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(-100f, y), new Vector2(150f, 78f));
            return button;
        }

        private static GameObject BuildMissionSelection(Transform parent)
        {
            GameObject panel = CreatePanel(parent, "MissionSelection", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 860f));

            Text title = CreateLabel(panel.transform, "Title", "PRACTICE", 44, TextAnchor.MiddleCenter);
            SetAnchors(title.rectTransform, new Vector2(0.5f, 0.94f), Vector2.zero, new Vector2(600f, 60f));

            Text coins = CreateLabel(panel.transform, "Coins", "0", 30, TextAnchor.MiddleRight);
            SetAnchors(coins.rectTransform, new Vector2(0.9f, 0.94f), Vector2.zero, new Vector2(200f, 40f));

            GameObject viewport = CreateChild(panel.transform, "Grid");
            RectTransform grid = viewport.GetComponent<RectTransform>();
            SetAnchors(grid, new Vector2(0.5f, 0.45f), Vector2.zero, new Vector2(1420f, 640f));

            var layout = viewport.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(330f, 140f);
            layout.spacing = new Vector2(16f, 16f);
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 4;

            Button close = CreateButton(panel.transform, "CloseButton", "CLOSE", new Vector2(0.5f, 0.05f), new Vector2(240f, 64f));
            close.onClick.AddListener(() => { });

            MissionCardView cardPrefab = CreateMissionCardPrefab();

            var selection = panel.AddComponent<MissionSelectionView>();
            SetPrivate(selection, "catalog", AssetDatabase.LoadAssetAtPath<MissionCatalog>(MissionCatalogPath));
            SetPrivate(selection, "cardPrefab", cardPrefab);
            SetPrivate(selection, "cardContainer", viewport.transform);
            SetPrivate(selection, "coinLabel", coins);

            // Closing the list is a plain panel toggle, wired without extra glue code.
            AddPanelToggle(close, panel, false);

            panel.SetActive(false);
            return panel;
        }

        private static GameObject BuildResultScreens(Transform parent)
        {
            GameObject host = CreateChild(parent, "MissionResult");
            Stretch(host);

            GameObject complete = CreatePanel(host.transform, "CompletePanel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 620f));
            CreateLabel(complete.transform, "Title", "MISSION COMPLETE", 44, TextAnchor.MiddleCenter).rectTransform.anchoredPosition = new Vector2(0f, 250f);

            // Star rating. The project has no star sprite usable in UI (the only star
            // textures are single-channel particle masks in the FX pack), so the built-in
            // round knob is used; swap the sprite when star art exists.
            Sprite starSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var resultStars = new Object[3];

            for (int i = 0; i < resultStars.Length; i++)
            {
                Image star = CreateImage(complete.transform, $"Star{i + 1}", new Color(1f, 1f, 1f, 0.2f));
                star.sprite = starSprite;
                SetAnchors(star.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 70f, 200f), new Vector2(52f, 52f));
                resultStars[i] = star;
            }

            Text score = CreateStatRow(complete.transform, "Score", "Score", 150f);
            Text time = CreateStatRow(complete.transform, "Time", "Time", 90f);
            Text collisions = CreateStatRow(complete.transform, "Collisions", "Collisions", 30f);
            Text penalty = CreateStatRow(complete.transform, "Penalty", "Time penalty", -30f);
            Text coins = CreateStatRow(complete.transform, "Coins", "Coins earned", -90f);

            Text best = CreateLabel(complete.transform, "NewBest", "NEW BEST", 26, TextAnchor.MiddleCenter);
            best.color = new Color(1f, 0.82f, 0.25f);
            best.rectTransform.anchoredPosition = new Vector2(0f, -140f);

            Text allDone = CreateLabel(complete.transform, "AllComplete", "All missions complete", 26, TextAnchor.MiddleCenter);
            allDone.rectTransform.anchoredPosition = new Vector2(0f, -180f);
            allDone.gameObject.SetActive(false);

            Button next = CreateButton(complete.transform, "NextButton", "NEXT", new Vector2(0.78f, 0.1f), new Vector2(220f, 70f));
            Button replay = CreateButton(complete.transform, "ReplayButton", "REPLAY", new Vector2(0.5f, 0.1f), new Vector2(220f, 70f));
            Button menu = CreateButton(complete.transform, "MenuButton", "MENU", new Vector2(0.22f, 0.1f), new Vector2(220f, 70f));

            GameObject failed = CreatePanel(host.transform, "FailedPanel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 460f));
            CreateLabel(failed.transform, "Title", "MISSION FAILED", 44, TextAnchor.MiddleCenter).rectTransform.anchoredPosition = new Vector2(0f, 160f);
            Text reason = CreateLabel(failed.transform, "Reason", "-", 30, TextAnchor.MiddleCenter);
            reason.rectTransform.anchoredPosition = new Vector2(0f, 60f);
            Text failScore = CreateStatRow(failed.transform, "Score", "Score", -10f);

            Button retry = CreateButton(failed.transform, "RetryButton", "RETRY", new Vector2(0.68f, 0.14f), new Vector2(240f, 70f));
            Button failMenu = CreateButton(failed.transform, "MenuButton", "MENU", new Vector2(0.32f, 0.14f), new Vector2(240f, 70f));

            var view = host.AddComponent<MissionResultView>();
            SetPrivate(view, "missionManager", Object.FindFirstObjectByType<MissionManager>());
            SetPrivate(view, "catalog", AssetDatabase.LoadAssetAtPath<MissionCatalog>(MissionCatalogPath));
            SetPrivate(view, "mainMenu", Object.FindFirstObjectByType<MainMenuManager>());
            SetPrivate(view, "completePanel", complete);
            SetPrivate(view, "failedPanel", failed);
            SetPrivate(view, "scoreLabel", score);
            SetPrivate(view, "timeLabel", time);
            SetPrivate(view, "collisionsLabel", collisions);
            SetPrivate(view, "penaltyLabel", penalty);
            SetPrivate(view, "coinsLabel", coins);
            SetPrivate(view, "newBestBadge", best.gameObject);
            SetPrivateArray(view, "starImages", resultStars);
            SetPrivate(view, "allMissionsCompleteMessage", allDone.gameObject);
            SetPrivate(view, "nextButton", next);
            SetPrivate(view, "replayButton", replay);
            SetPrivate(view, "menuButton", menu);
            SetPrivate(view, "failReasonLabel", reason);
            SetPrivate(view, "failScoreLabel", failScore);
            SetPrivate(view, "retryButton", retry);
            SetPrivate(view, "failMenuButton", failMenu);

            complete.SetActive(false);
            failed.SetActive(false);
            return host;
        }

        // A single always-visible button that opens the mission list, so the new pipeline is
        // reachable without touching the legacy menu's wiring.
        private static GameObject BuildOpenButton(Transform parent, GameObject missionList)
        {
            Button open = CreateButton(parent, "OpenPracticeButton", "PRACTICE (NEW)", new Vector2(0f, 0f), new Vector2(300f, 70f));
            SetAnchors(open.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(170f, 60f), new Vector2(300f, 70f));
            AddPanelToggle(open, missionList, true);
            return open.gameObject;
        }

        private static GameObject BuildGaragePanel(Transform parent)
        {
            GameObject panel = CreatePanel(parent, "Garage", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 780f));

            CreateLabel(panel.transform, "Title", "GARAGE", 44, TextAnchor.MiddleCenter).rectTransform.anchoredPosition = new Vector2(0f, 330f);

            Text carName = CreateLabel(panel.transform, "CarName", "-", 36, TextAnchor.MiddleCenter);
            carName.rectTransform.anchoredPosition = new Vector2(0f, 250f);

            Text coins = CreateLabel(panel.transform, "Coins", "0", 30, TextAnchor.MiddleRight);
            SetAnchors(coins.rectTransform, new Vector2(0.88f, 0.94f), Vector2.zero, new Vector2(200f, 40f));

            Text price = CreateLabel(panel.transform, "Price", string.Empty, 30, TextAnchor.MiddleCenter);
            price.rectTransform.anchoredPosition = new Vector2(0f, -60f);

            Text stats = CreateLabel(panel.transform, "Stats", "-", 24, TextAnchor.MiddleLeft);
            SetAnchors(stats.rectTransform, new Vector2(0.3f, 0.5f), new Vector2(0f, 110f), new Vector2(320f, 160f));

            Text lockedBadge = CreateLabel(panel.transform, "LockedBadge", "LOCKED", 26, TextAnchor.MiddleCenter);
            lockedBadge.color = new Color(1f, 0.5f, 0.4f);
            lockedBadge.rectTransform.anchoredPosition = new Vector2(0f, 190f);

            Text selectedBadge = CreateLabel(panel.transform, "SelectedBadge", "SELECTED", 26, TextAnchor.MiddleCenter);
            selectedBadge.color = new Color(0.4f, 0.9f, 0.5f);
            selectedBadge.rectTransform.anchoredPosition = new Vector2(0f, 190f);

            Button previous = CreateButton(panel.transform, "PreviousButton", "<", new Vector2(0.08f, 0.5f), new Vector2(110f, 110f));
            Button next = CreateButton(panel.transform, "NextButton", ">", new Vector2(0.92f, 0.5f), new Vector2(110f, 110f));
            Button buy = CreateButton(panel.transform, "BuyButton", "BUY", new Vector2(0.38f, 0.12f), new Vector2(240f, 76f));
            Button select = CreateButton(panel.transform, "SelectButton", "SELECT", new Vector2(0.62f, 0.12f), new Vector2(240f, 76f));

            // Colour swatches. The palette has eight entries, so eight buttons.
            var colorButtons = new Object[8];

            for (int i = 0; i < colorButtons.Length; i++)
            {
                Button swatch = CreateButton(panel.transform, $"Color{i + 1}", string.Empty, new Vector2(0.5f, 0.28f), new Vector2(64f, 64f));
                SetAnchors(swatch.GetComponent<RectTransform>(), new Vector2(0.5f, 0.28f), new Vector2((i - 3.5f) * 76f, 0f), new Vector2(64f, 64f));
                colorButtons[i] = swatch;
            }

            Button close = CreateButton(panel.transform, "CloseButton", "CLOSE", new Vector2(0.5f, 0.04f), new Vector2(220f, 60f));
            AddPanelToggle(close, panel, false);

            var view = panel.AddComponent<GarageView>();
            SetPrivate(view, "garage", Object.FindFirstObjectByType<GarageManager>(FindObjectsInactive.Include));
            SetPrivate(view, "carNameLabel", carName);
            SetPrivate(view, "coinLabel", coins);
            SetPrivate(view, "priceLabel", price);
            SetPrivate(view, "statsLabel", stats);
            SetPrivate(view, "previousButton", previous);
            SetPrivate(view, "nextButton", next);
            SetPrivate(view, "buyButton", buy);
            SetPrivate(view, "selectButton", select);
            SetPrivate(view, "lockedBadge", lockedBadge.gameObject);
            SetPrivate(view, "selectedBadge", selectedBadge.gameObject);
            SetPrivateArray(view, "colorButtons", colorButtons);

            panel.SetActive(false);
            return panel;
        }

        private static GameObject BuildSettingsPanel(Transform parent)
        {
            GameObject panel = CreatePanel(parent, "Settings", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 760f));

            CreateLabel(panel.transform, "Title", "SETTINGS", 44, TextAnchor.MiddleCenter).rectTransform.anchoredPosition = new Vector2(0f, 320f);

            Slider music = CreateSlider(panel.transform, "MusicSlider", "Music", 200f, 0f, 1f);
            Slider sfx = CreateSlider(panel.transform, "SfxSlider", "SFX", 120f, 0f, 1f);
            Slider sensitivity = CreateSlider(panel.transform, "SensitivitySlider", "Steering", 40f, 0.25f, 3f);

            CreateLabel(panel.transform, "QualityCaption", "Graphics", 26, TextAnchor.MiddleLeft)
                .rectTransform.anchoredPosition = new Vector2(-380f, -50f);

            Button low = CreateButton(panel.transform, "LowButton", "LOW", new Vector2(0.5f, 0.5f), new Vector2(170f, 68f));
            SetAnchors(low.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-200f, -110f), new Vector2(170f, 68f));
            Button medium = CreateButton(panel.transform, "MediumButton", "MEDIUM", new Vector2(0.5f, 0.5f), new Vector2(170f, 68f));
            SetAnchors(medium.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(170f, 68f));
            Button high = CreateButton(panel.transform, "HighButton", "HIGH", new Vector2(0.5f, 0.5f), new Vector2(170f, 68f));
            SetAnchors(high.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(200f, -110f), new Vector2(170f, 68f));

            Text qualityLabel = CreateLabel(panel.transform, "QualityValue", "-", 24, TextAnchor.MiddleRight);
            qualityLabel.rectTransform.anchoredPosition = new Vector2(380f, -50f);

            CreateLabel(panel.transform, "FpsCaption", "Frame rate", 26, TextAnchor.MiddleLeft)
                .rectTransform.anchoredPosition = new Vector2(-380f, -190f);

            Button fps30 = CreateButton(panel.transform, "Fps30Button", "30", new Vector2(0.5f, 0.5f), new Vector2(170f, 68f));
            SetAnchors(fps30.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-100f, -250f), new Vector2(170f, 68f));
            Button fps60 = CreateButton(panel.transform, "Fps60Button", "60", new Vector2(0.5f, 0.5f), new Vector2(170f, 68f));
            SetAnchors(fps60.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(100f, -250f), new Vector2(170f, 68f));

            Text fpsLabel = CreateLabel(panel.transform, "FpsValue", "-", 24, TextAnchor.MiddleRight);
            fpsLabel.rectTransform.anchoredPosition = new Vector2(380f, -190f);

            Button reset = CreateButton(panel.transform, "ResetButton", "RESET PROGRESS", new Vector2(0.5f, 0.5f), new Vector2(340f, 64f));
            SetAnchors(reset.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -320f), new Vector2(340f, 64f));

            // Reset is behind a confirmation step, because it wipes the player's progress.
            GameObject confirm = CreatePanel(panel.transform, "ResetConfirm", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 260f));
            CreateLabel(confirm.transform, "ConfirmText", "Erase all progress?", 30, TextAnchor.MiddleCenter)
                .rectTransform.anchoredPosition = new Vector2(0f, 50f);
            Button confirmYes = CreateButton(confirm.transform, "ConfirmButton", "ERASE", new Vector2(0.3f, 0.25f), new Vector2(200f, 66f));
            Button confirmNo = CreateButton(confirm.transform, "CancelButton", "CANCEL", new Vector2(0.7f, 0.25f), new Vector2(200f, 66f));
            confirm.SetActive(false);

            Button close = CreateButton(panel.transform, "CloseButton", "CLOSE", new Vector2(0.5f, 0.04f), new Vector2(220f, 60f));
            AddPanelToggle(close, panel, false);

            var view = panel.AddComponent<SettingsView>();
            SetPrivate(view, "settings", Object.FindFirstObjectByType<SettingsManager>(FindObjectsInactive.Include));
            SetPrivate(view, "musicSlider", music);
            SetPrivate(view, "sfxSlider", sfx);
            SetPrivate(view, "sensitivitySlider", sensitivity);
            SetPrivate(view, "lowQualityButton", low);
            SetPrivate(view, "mediumQualityButton", medium);
            SetPrivate(view, "highQualityButton", high);
            SetPrivate(view, "frameRate30Button", fps30);
            SetPrivate(view, "frameRate60Button", fps60);
            SetPrivate(view, "qualityLabel", qualityLabel);
            SetPrivate(view, "frameRateLabel", fpsLabel);
            SetPrivate(view, "resetProgressButton", reset);
            SetPrivate(view, "resetConfirmPanel", confirm);
            SetPrivate(view, "resetConfirmButton", confirmYes);
            SetPrivate(view, "resetCancelButton", confirmNo);

            panel.SetActive(false);
            return panel;
        }

        private static Slider CreateSlider(Transform parent, string name, string caption, float y, float min, float max)
        {
            CreateLabel(parent, name + "Caption", caption, 26, TextAnchor.MiddleLeft)
                .rectTransform.anchoredPosition = new Vector2(-380f, y);

            GameObject sliderObject = CreateChild(parent, name);
            SetAnchors(sliderObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(80f, y), new Vector2(520f, 36f));

            Image background = CreateImage(sliderObject.transform, "Background", new Color(1f, 1f, 1f, 0.15f));
            Stretch(background.gameObject);

            GameObject fillArea = CreateChild(sliderObject.transform, "FillArea");
            Stretch(fillArea);
            Image fill = CreateImage(fillArea.transform, "Fill", AccentColor);
            Stretch(fill.gameObject);

            GameObject handleArea = CreateChild(sliderObject.transform, "HandleArea");
            Stretch(handleArea);
            Image handle = CreateImage(handleArea.transform, "Handle", Color.white);
            handle.rectTransform.sizeDelta = new Vector2(30f, 44f);

            var slider = sliderObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = Mathf.Clamp(1f, min, max);

            return slider;
        }

        private static GameObject BuildMenuButton(Transform parent, string name, string caption, float y, GameObject panel)
        {
            Button button = CreateButton(parent, name, caption, new Vector2(0f, 0f), new Vector2(300f, 70f));
            SetAnchors(button.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(170f, y), new Vector2(300f, 70f));
            AddPanelToggle(button, panel, true);
            return button.gameObject;
        }

        private static MissionCardView CreateMissionCardPrefab()
        {
            const string folder = "Assets/GameAssets/Prefabs/UI";
            const string path = folder + "/MissionCard.prefab";

            if (!AssetDatabase.IsValidFolder("Assets/GameAssets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets/GameAssets", "Prefabs");
            }

            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets/GameAssets/Prefabs", "UI");
            }

            var card = new GameObject("MissionCard", typeof(RectTransform), typeof(Image), typeof(Button));
            card.GetComponent<Image>().color = ButtonColor;
            card.GetComponent<RectTransform>().sizeDelta = new Vector2(330f, 140f);

            Text number = CreateLabel(card.transform, "Number", "01", 40, TextAnchor.UpperLeft);
            SetAnchors(number.rectTransform, new Vector2(0.15f, 0.75f), Vector2.zero, new Vector2(90f, 50f));

            Text name = CreateLabel(card.transform, "Name", "Mission", 24, TextAnchor.UpperLeft);
            SetAnchors(name.rectTransform, new Vector2(0.58f, 0.75f), Vector2.zero, new Vector2(200f, 50f));

            Text difficulty = CreateLabel(card.transform, "Difficulty", "*", 22, TextAnchor.MiddleLeft);
            SetAnchors(difficulty.rectTransform, new Vector2(0.2f, 0.4f), Vector2.zero, new Vector2(120f, 30f));

            Text bestScore = CreateLabel(card.transform, "BestScore", string.Empty, 20, TextAnchor.MiddleRight);
            SetAnchors(bestScore.rectTransform, new Vector2(0.78f, 0.4f), Vector2.zero, new Vector2(150f, 30f));

            var starImages = new Image[3];

            for (int i = 0; i < 3; i++)
            {
                Image star = CreateImage(card.transform, $"Star{i + 1}", new Color(1f, 1f, 1f, 0.2f));
                SetAnchors(star.rectTransform, new Vector2(0.3f + i * 0.14f, 0.15f), Vector2.zero, new Vector2(26f, 26f));
                starImages[i] = star;
            }

            GameObject locked = CreateImage(card.transform, "LockedOverlay", new Color(0f, 0f, 0f, 0.65f)).gameObject;
            Stretch(locked);
            CreateLabel(locked.transform, "LockedLabel", "LOCKED", 24, TextAnchor.MiddleCenter);

            var view = card.AddComponent<MissionCardView>();
            SetPrivate(view, "button", card.GetComponent<Button>());
            SetPrivate(view, "numberLabel", number);
            SetPrivate(view, "nameLabel", name);
            SetPrivate(view, "bestScoreLabel", bestScore);
            SetPrivate(view, "difficultyLabel", difficulty);
            SetPrivate(view, "lockedOverlay", locked);
            SetPrivateArray(view, "starImages", starImages);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(card, path);
            Object.DestroyImmediate(card);

            Debug.Log($"[GameplayUiBuilder] Wrote the mission card prefab to '{path}'.");
            return saved.GetComponent<MissionCardView>();
        }

        private static void AddPanelToggle(Button button, GameObject panel, bool show)
        {
            var toggle = button.gameObject.AddComponent<PanelToggleButton>();
            SetPrivate(toggle, "panel", panel);
            SetPrivate(toggle, "show", show);
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child;
        }

        private static RectTransform Stretch(GameObject target)
        {
            var rect = target.GetComponent<RectTransform>();

            if (rect == null)
            {
                rect = target.AddComponent<RectTransform>();
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            GameObject panel = CreateChild(parent, name);
            var image = panel.AddComponent<Image>();
            image.color = PanelColor;

            SetAnchors(panel.GetComponent<RectTransform>(), anchor, position, size);
            return panel;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject target = CreateChild(parent, name);
            var image = target.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateLabel(Transform parent, string name, string content, int fontSize, TextAnchor alignment)
        {
            GameObject target = CreateChild(parent, name);
            var label = target.AddComponent<Text>();

            label.text = content;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            label.font = LegacyFont;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            SetAnchors(label.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 40f));
            return label;
        }

        private static Text CreateStatRow(Transform parent, string name, string caption, float y)
        {
            Text label = CreateLabel(parent, name + "Caption", caption, 26, TextAnchor.MiddleLeft);
            SetAnchors(label.rectTransform, new Vector2(0.3f, 0.5f), new Vector2(0f, y), new Vector2(320f, 36f));

            Text value = CreateLabel(parent, name, "-", 30, TextAnchor.MiddleRight);
            SetAnchors(value.rectTransform, new Vector2(0.72f, 0.5f), new Vector2(0f, y), new Vector2(260f, 36f));
            return value;
        }

        private static Button CreateButton(Transform parent, string name, string caption, Vector2 anchor, Vector2 size)
        {
            GameObject target = CreateChild(parent, name);
            var image = target.AddComponent<Image>();
            image.color = ButtonColor;

            var button = target.AddComponent<Button>();
            button.targetGraphic = image;

            SetAnchors(target.GetComponent<RectTransform>(), anchor, Vector2.zero, size);

            Text label = CreateLabel(target.transform, "Label", caption, 26, TextAnchor.MiddleCenter);
            Stretch(label.gameObject);

            return button;
        }

        private static void SetAnchors(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Font LegacyFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private static void SetPrivate(Object target, string fieldName, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogWarning($"[GameplayUiBuilder] No field '{fieldName}' on {target.GetType().Name}.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }

        private static void SetPrivate(Object target, string fieldName, bool value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogWarning($"[GameplayUiBuilder] No field '{fieldName}' on {target.GetType().Name}.");
                return;
            }

            property.boolValue = value;
            serialized.ApplyModifiedProperties();
        }

        private static void SetPrivateArray(Object target, string fieldName, Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogWarning($"[GameplayUiBuilder] No field '{fieldName}' on {target.GetType().Name}.");
                return;
            }

            property.arraySize = values.Length;

            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedProperties();
        }
    }
}
