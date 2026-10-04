using System.Collections.Generic;
using CarParkingGame.Core;
using CarParkingGame.Garage;
using CarParkingGame.Missions;
using CarParkingGame.Settings;
using CarParkingGame.UI;
using CarParkingGame.Vehicle;
using SimpleInputNamespace;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CarParkingGame.EditorTools
{
    // Builds the whole front end and HUD into one canvas.
    //
    // Everything the player sees now comes from here, which is the point: the previous
    // version added a second canvas on top of the legacy menu, so the two drew over each
    // other - that is the overlapping screens in the bug report. The legacy MainMenuCanvas
    // and MissionInfo canvas are switched off instead of being worked around.
    //
    // Three rules keep the result free of overlap:
    //  - every menu screen is a sibling under MenuRoot, and MenuController activates
    //    exactly one of them,
    //  - the HUD and MenuRoot are mutually exclusive, driven by GameSession, not by
    //    Time.timeScale,
    //  - nothing is positioned by eye twice: the shared constants below are the layout.
    //
    // The driving controls are not rebuilt. SimpleInput's steering wheel, pedals and brake
    // are moved out of the legacy canvas into this one, so their art and their input
    // wiring survive while their positions come under this file's control.
    public static class GameplayUiBuilder
    {
        private const string ScenePath = "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity";
        private const string CanvasName = "GameUI";
        private const string LegacyCanvasName = "NewGameplayUI";
        private const string MissionCatalogPath = "Assets/GameAssets/ScriptableObjects/MissionCatalog.asset";
        private const string CardPrefabPath = "Assets/GameAssets/Prefabs/UI/MissionCard.prefab";

        // ----- layout constants -------------------------------------------------------------

        private static readonly Vector2 Reference = new Vector2(1920f, 1080f);

        // The brake pedal, and the ring of car controls drawn around it.
        private static readonly Vector2 BrakeAnchoredPosition = new Vector2(-600f, 300f);
        private const float BrakeSize = 250f;
        private const float RingRadius = 230f;
        private const float RingButtonSize = 112f;

        // ----- palette ----------------------------------------------------------------------

        private static readonly Color Scrim = new Color(0.04f, 0.05f, 0.08f, 0.82f);
        private static readonly Color Dim = new Color(0.02f, 0.03f, 0.05f, 0.72f);
        private static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.13f, 0.95f);
        private static readonly Color CardColor = new Color(0.13f, 0.15f, 0.2f, 0.96f);
        private static readonly Color ButtonColor = new Color(0.17f, 0.2f, 0.27f, 0.96f);
        private static readonly Color AccentColor = new Color(0.16f, 0.6f, 1f, 1f);
        private static readonly Color WarnColor = new Color(0.95f, 0.45f, 0.2f, 1f);
        private static readonly Color HudGlass = new Color(0.03f, 0.04f, 0.07f, 0.6f);
        private static readonly Color IconIdle = new Color(1f, 1f, 1f, 0.85f);

        [MenuItem("Tools/Car Parking/Build Game UI")]
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
            IconSpriteGenerator.Generate();

            // The driving controls live inside the canvas that is about to be deleted, and
            // deleting them would take SimpleInput's wiring with them. They are lifted out
            // first and put back by BuildDrivingControls.
            DetachDrivingControls();

            DestroyIfPresent(LegacyCanvasName);
            DestroyIfPresent(CanvasName);

            GameObject canvasObject = CreateCanvas();
            Transform canvas = canvasObject.transform;

            GameSession session = CreateSession();
            SilenceLegacyUi();

            GameObject hud = BuildHud(canvas, session);
            GameObject menuRoot = BuildMenu(canvas, session, out MenuController menu);
            GameObject pause = BuildPauseScreen(canvas, session, canvasObject);
            BuildResultScreens(canvas);

            // The HUD belongs to play and the menu to the menu; one component each, driven
            // by the session rather than by the clock.
            AddVisibility(canvasObject, new Object[] { hud }, false);
            AddVisibility(canvasObject, new Object[] { menuRoot }, true);

            SetPrivate(menu, "menuRoot", menuRoot);

            // The pause screen borrows the menu own settings screen rather than keeping a
            // second copy of it, so it needs to know where the menu lives.
            var pauseView = canvasObject.GetComponent<PauseMenuView>();
            SetPrivate(pauseView, "menu", menu);
            SetPrivate(pauseView, "menuRoot", menuRoot);

            BuildScreenFade(canvas, canvasObject);

            WireSessionObjects(session, hud, pause);
            EnableGarageManager();

            Debug.Log("[GameplayUiBuilder] Built the home screen, mode select, practice list, garage, settings, HUD, pause and result screens under one canvas. " +
                      "The legacy MainMenuCanvas and MissionInfo canvas are switched off.");
        }

        // ----- scene-level objects ------------------------------------------------------------

        private static GameSession CreateSession()
        {
            GameSession session = Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);

            if (session == null)
            {
                var host = new GameObject("GameSession");
                Undo.RegisterCreatedObjectUndo(host, "Create GameSession");
                session = host.AddComponent<GameSession>();
            }

            GameObject menuCamera = FindMenuCamera();
            GameObject gameplayCamera = Camera.main != null ? Camera.main.gameObject : null;
            GameObject showroom = FindShowroomContainer();
            GameObject playerCars = FindPlayerCarContainer();

            SetPrivate(session, "missions", Object.FindFirstObjectByType<MissionManager>(FindObjectsInactive.Include));
            SetPrivate(session, "menuCamera", menuCamera);
            SetPrivate(session, "gameplayCamera", gameplayCamera);
            SetPrivate(session, "showroomCars", showroom);
            SetPrivate(session, "playerCars", playerCars);

            ShowroomCameraRig rig = session.GetComponent<ShowroomCameraRig>();

            if (rig == null)
            {
                rig = session.gameObject.AddComponent<ShowroomCameraRig>();
            }

            SetPrivate(rig, "menuCamera", menuCamera != null ? menuCamera.transform : null);
            SetPrivate(rig, "showroomCars", showroom != null ? showroom.transform : null);

            // Written explicitly rather than left to the component's own defaults: the rig
            // survives a rebuild, and a value already serialized into the scene does not
            // change when the field's default does.
            //
            // Both framings look from the same side of the showroom. Round the other side
            // there is a lamp post and a grandstand within a couple of metres of the car,
            // and the camera ended up inside them.
            SetPrivate(rig, "homeYaw", 125f);
            SetPrivate(rig, "homePitch", 9f);
            SetPrivate(rig, "homeDistanceFactor", 2.5f);
            SetPrivate(rig, "garageYaw", 125f);
            SetPrivate(rig, "garagePitch", 9f);
            SetPrivate(rig, "garageDistanceFactor", 2.3f);
            SetPrivate(rig, "screenOffset", 0.26f);
            SetPrivate(rig, "aimDrop", 0.3f);

            return session;
        }

        // The legacy menu drew an opaque black rectangle over the whole screen, which is
        // why the showroom car was never visible behind it. Both legacy canvases go.
        private static void SilenceLegacyUi()
        {
            var menu = Object.FindFirstObjectByType<MainMenuManager>(FindObjectsInactive.Include);

            if (menu != null)
            {
                menu.gameObject.SetActive(false);
                EditorUtility.SetDirty(menu.gameObject);
            }

            GameObject missionInfo = GameObject.Find("MissionInfo");

            if (missionInfo != null)
            {
                missionInfo.SetActive(false);
                EditorUtility.SetDirty(missionInfo);
            }
        }

        private static void EnableGarageManager()
        {
            var garage = Object.FindFirstObjectByType<GarageManager>(FindObjectsInactive.Include);

            if (garage == null)
            {
                Debug.LogWarning("[GameplayUiBuilder] No GarageManager in the scene; run Wire Gameplay Scene first.");
                return;
            }

            garage.enabled = true;
            EditorUtility.SetDirty(garage);
        }

        private static GameObject CreateCanvas()
        {
            var canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create game UI");

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            // Landscape: matching height keeps everything the same size on tall phones.
            scaler.matchWidthOrHeight = 1f;

            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include) == null)
            {
                var eventSystem = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));

                Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
            }

            return canvasObject;
        }

        // ================= HUD ==================================================================

        private static GameObject BuildHud(Transform parent, GameSession session)
        {
            GameObject hud = CreateChild(parent, "HUD");
            Stretch(hud);

            BuildHudReadouts(hud, out Text missionName, out Text score, out Text coins, out Text timer, out GameObject timerChip);
            BuildBayCounter(hud, out Text bayCount, out GameObject bayChip);
            BuildRearViewMirror(hud);
            BuildSpeedometer(hud);
            BuildParkingFeedback(hud, out Image indicator, out Image progress, out Text hint);
            BuildGuideArrow(hud);
            GameObject controls = BuildDrivingControls(hud);

            var hudView = hud.AddComponent<GameplayHudView>();
            SetPrivate(hudView, "missionNameLabel", missionName);
            SetPrivate(hudView, "scoreLabel", score);
            SetPrivate(hudView, "coinLabel", coins);
            SetPrivate(hudView, "timerLabel", timer);
            SetPrivate(hudView, "timerContainer", timerChip);
            SetPrivate(hudView, "bayCountLabel", bayCount);
            SetPrivate(hudView, "bayCountContainer", bayChip);
            SetPrivate(hudView, "parkingStateIndicator", indicator);
            SetPrivate(hudView, "parkingProgressBar", progress);
            SetPrivate(hudView, "parkingHintLabel", hint);
            SetPrivate(hudView, "missionManager", Object.FindFirstObjectByType<MissionManager>(FindObjectsInactive.Include));

            SetPrivate(session, "mobileControls", controls);

            return hud;
        }

        private static void BuildHudReadouts(
            GameObject hud,
            out Text missionName,
            out Text score,
            out Text coins,
            out Text timer,
            out GameObject timerChip)
        {
            GameObject info = CreatePanel(hud.transform, "InfoPanel", new Vector2(0f, 1f), new Vector2(460f, -78f), new Vector2(600f, 116f), HudGlass);

            missionName = CreateLabel(info.transform, "MissionName", "-", 30, TextAnchor.MiddleLeft);
            SetRect(missionName.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -32f), new Vector2(540f, 40f), new Vector2(0f, 1f));

            Image scoreIcon = CreateIcon(info.transform, "ScoreIcon", "cone", new Color(1f, 1f, 1f, 0.6f), 26f);
            SetRect(scoreIcon.rectTransform, new Vector2(0f, 0f), new Vector2(34f, 32f), new Vector2(26f, 26f), new Vector2(0.5f, 0.5f));

            score = CreateLabel(info.transform, "Score", "100", 32, TextAnchor.MiddleLeft);
            SetRect(score.rectTransform, new Vector2(0f, 0f), new Vector2(58f, 32f), new Vector2(140f, 36f), new Vector2(0f, 0.5f));

            Image coinIcon = CreateIcon(info.transform, "CoinIcon", "disc", new Color(1f, 0.82f, 0.25f, 0.95f), 22f);
            SetRect(coinIcon.rectTransform, new Vector2(1f, 0f), new Vector2(-190f, 32f), new Vector2(22f, 22f), new Vector2(0.5f, 0.5f));

            coins = CreateLabel(info.transform, "Coins", "0", 32, TextAnchor.MiddleLeft);
            SetRect(coins.rectTransform, new Vector2(1f, 0f), new Vector2(-168f, 32f), new Vector2(160f, 36f), new Vector2(0f, 0.5f));

            // The clock is its own chip because untimed missions hide it, and a hole in the
            // middle of the info panel would look like a bug.
            timerChip = CreatePanel(hud.transform, "TimerChip", new Vector2(0f, 1f), new Vector2(460f, -172f), new Vector2(220f, 62f), HudGlass);
            Image clockIcon = CreateIcon(timerChip.transform, "ClockIcon", "stopwatch", new Color(1f, 1f, 1f, 0.75f), 30f);
            SetRect(clockIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(38f, 0f), new Vector2(30f, 30f), new Vector2(0.5f, 0.5f));

            timer = CreateLabel(timerChip.transform, "Timer", "0:00", 32, TextAnchor.MiddleLeft);
            SetRect(timer.rectTransform, new Vector2(0f, 0.5f), new Vector2(66f, 0f), new Vector2(140f, 40f), new Vector2(0f, 0.5f));
        }

        // The P 0/3 counter, for the courses that ask for more than one bay. Top right,
        // beside the speedometer, which is where a player looks for "how much is left".
        private static void BuildBayCounter(GameObject hud, out Text label, out GameObject chip)
        {
            chip = CreatePanel(hud.transform, "BayChip", new Vector2(1f, 1f), new Vector2(-372f, -90f), new Vector2(188f, 76f), HudGlass);

            Text badge = CreateLabel(chip.transform, "Badge", "P", 40, TextAnchor.MiddleCenter);
            badge.color = new Color(0.55f, 0.78f, 1f, 1f);
            SetRect(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(42f, 0f), new Vector2(44f, 48f), new Vector2(0.5f, 0.5f));

            label = CreateLabel(chip.transform, "BayCount", "0/1", 36, TextAnchor.MiddleLeft);
            SetRect(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(76f, 0f), new Vector2(110f, 44f), new Vector2(0f, 0.5f));

            // Off until a mission says otherwise, so a one-bay course never shows it.
            chip.SetActive(false);
        }

        // The three mirrors across the top of the screen - door mirror, rear-view, door
        // mirror - and the component that renders the world behind the car into them.
        //
        // Offset right of centre rather than dead centre: the mission panel runs to
        // x = 760 on the left and the bay counter starts at 1454 on the right, so a group
        // centred on the canvas would sit on top of the coin count.
        private static void BuildRearViewMirror(GameObject hud)
        {
            GameObject group = CreateChild(hud.transform, "RearMirrors");
            SetRect(group.GetComponent<RectTransform>(), new Vector2(0.5f, 1f),
                new Vector2(142f, -78f), new Vector2(672f, 136f), new Vector2(0.5f, 0.5f));

            RawImage left = MirrorGlass(group.transform, "LeftMirror", new Vector2(0f, 0.5f),
                new Vector2(100f, -6f), new Vector2(196f, 112f));

            RawImage centre = MirrorGlass(group.transform, "RearMirror", new Vector2(0.5f, 0.5f),
                new Vector2(0f, 2f), new Vector2(256f, 126f));

            RawImage right = MirrorGlass(group.transform, "RightMirror", new Vector2(1f, 0.5f),
                new Vector2(-100f, -6f), new Vector2(196f, 112f));

            var mirror = hud.AddComponent<RearViewMirror>();
            SetPrivate(mirror, "frame", group);
            SetPrivate(mirror, "centreGlass", centre);
            SetPrivate(mirror, "leftGlass", left);
            SetPrivate(mirror, "rightGlass", right);
            SetPrivate(mirror, "director", Object.FindFirstObjectByType<VehicleCameraDirector>(FindObjectsInactive.Include));

            // Off until the player is sitting in a car.
            group.SetActive(false);
        }

        // One mirror: a dark bezel with the glass inset into it.
        private static RawImage MirrorGlass(Transform parent, string name, Vector2 anchor,
            Vector2 position, Vector2 size)
        {
            GameObject bezel = CreatePanel(parent, name, anchor, position, size, new Color(0.07f, 0.07f, 0.08f, 0.95f));

            GameObject surface = CreateChild(bezel.transform, "Glass");
            var glass = surface.AddComponent<RawImage>();

            SetRect(glass.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero,
                size - new Vector2(16f, 16f), new Vector2(0.5f, 0.5f));

            // Nothing to press. Left on, a mirror would eat taps meant for the HUD under it.
            glass.raycastTarget = false;

            return glass;
        }

        private static void BuildSpeedometer(GameObject hud)
        {
            GameObject panel = CreatePanel(hud.transform, "SpeedPanel", new Vector2(1f, 1f), new Vector2(-140f, -90f), new Vector2(200f, 130f), HudGlass);

            Text speed = CreateLabel(panel.transform, "Speed", "0", 60, TextAnchor.MiddleCenter);
            SetRect(speed.rectTransform, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(180f, 66f), new Vector2(0.5f, 0.5f));

            Text unit = CreateLabel(panel.transform, "Unit", "km/h", 22, TextAnchor.MiddleCenter);
            unit.color = new Color(1f, 1f, 1f, 0.6f);
            SetRect(unit.rectTransform, new Vector2(0.5f, 0.22f), Vector2.zero, new Vector2(180f, 28f), new Vector2(0.5f, 0.5f));

            var speedometer = hud.AddComponent<SpeedometerView>();
            SetPrivate(speedometer, "speedLabel", speed);
            SetPrivate(speedometer, "unitLabel", unit);
        }

        private static void BuildParkingFeedback(GameObject hud, out Image indicator, out Image progress, out Text hint)
        {
            GameObject panel = CreatePanel(hud.transform, "ParkingFeedback", new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(660f, 104f), HudGlass);

            indicator = CreateImage(panel.transform, "StateIndicator", new Color(0.8f, 0.25f, 0.25f));
            SetRect(indicator.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(600f, 12f), new Vector2(0.5f, 0.5f));

            Image track = CreateImage(panel.transform, "ProgressTrack", new Color(1f, 1f, 1f, 0.12f));
            SetRect(track.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(600f, 16f), new Vector2(0.5f, 0.5f));

            progress = CreateImage(track.transform, "ProgressBar", AccentColor);

            // A Filled image with no sprite silently renders as a full quad, so the bar
            // would never move. Any sprite fixes it.
            progress.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            progress.type = Image.Type.Filled;
            progress.fillMethod = Image.FillMethod.Horizontal;
            progress.fillAmount = 0f;
            Stretch(progress.gameObject);

            hint = CreateLabel(panel.transform, "Hint", string.Empty, 24, TextAnchor.MiddleCenter);
            SetRect(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(640f, 32f), new Vector2(0.5f, 0.5f));
        }

        private static void BuildGuideArrow(GameObject hud)
        {
            GameObject host = CreateChild(hud.transform, "GuideArrow");
            Stretch(host);

            GameObject arrow = CreateChild(host.transform, "Arrow");
            SetRect(arrow.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f), new Vector2(0.5f, 0.5f));

            Image sprite = CreateIcon(arrow.transform, "Icon", "guide-arrow", new Color(1f, 0.85f, 0.2f, 0.95f), 120f);
            Stretch(sprite.gameObject);

            Text distance = CreateLabel(arrow.transform, "Distance", "0 m", 26, TextAnchor.MiddleCenter);
            SetRect(distance.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -22f), new Vector2(200f, 32f), new Vector2(0.5f, 0.5f));

            var guide = host.AddComponent<BayGuideArrow>();
            SetPrivate(guide, "missions", Object.FindFirstObjectByType<MissionManager>(FindObjectsInactive.Include));
            SetPrivate(guide, "arrow", arrow.GetComponent<RectTransform>());
            SetPrivate(guide, "distanceLabel", distance);
            SetPrivate(guide, "worldCamera", Camera.main);

            // Shown in every mode, not just challenge: a player dropped at the start of a
            // practice course has no more idea where the bay is than one halfway round a
            // challenge run.
            SetPrivate(guide, "challengeOnly", false);

            arrow.SetActive(false);
        }

        // ----- driving controls ---------------------------------------------------------------

        // SimpleInput's widgets are moved rather than rebuilt: their sprites and their
        // serialized axis names are the working input setup, and recreating them would mean
        // reproducing both by hand.
        private static GameObject BuildDrivingControls(GameObject hud)
        {
            GameObject controls = CreateChild(hud.transform, "DrivingControls");
            Stretch(controls);

            MoveSteeringWheel(controls.transform);
            MovePedals(controls.transform);
            RectTransform brake = MoveBrake(controls.transform);
            BuildControlRing(controls.transform, brake);

            RetireLegacyControlCanvas();
            return controls;
        }

        private static void MoveSteeringWheel(Transform parent)
        {
            var wheel = Object.FindFirstObjectByType<SteeringWheel>(FindObjectsInactive.Include);

            if (wheel == null)
            {
                Debug.LogWarning("[GameplayUiBuilder] No SimpleInput SteeringWheel found; the steering control was left where it was.");
                return;
            }

            Reparent(wheel.transform, parent);
            SetRect(wheel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(300f, 280f), new Vector2(460f, 460f), new Vector2(0.5f, 0.5f));
        }

        private static void MovePedals(Transform parent)
        {
            var arrows = Object.FindFirstObjectByType<AxisInputUIArrows>(FindObjectsInactive.Include);

            if (arrows == null)
            {
                Debug.LogWarning("[GameplayUiBuilder] No SimpleInput arrows found; throttle and reverse were left where they were.");
                return;
            }

            Reparent(arrows.transform, parent);
            RectTransform rect = arrows.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(1f, 0f), new Vector2(-180f, 330f), new Vector2(200f, 460f), new Vector2(0.5f, 0.5f));

            // The two halves keep their own sizes, so they are re-spaced to the new height.
            for (int i = 0; i < rect.childCount; i++)
            {
                RectTransform child = rect.GetChild(i) as RectTransform;

                if (child == null)
                {
                    continue;
                }

                bool top = child.name.IndexOf("top", System.StringComparison.OrdinalIgnoreCase) >= 0;
                SetRect(child, new Vector2(0.5f, 0.5f), new Vector2(0f, top ? 125f : -125f), new Vector2(190f, 190f), new Vector2(0.5f, 0.5f));
            }
        }

        private static RectTransform MoveBrake(Transform parent)
        {
            var brake = Object.FindFirstObjectByType<ButtonInputUI>(FindObjectsInactive.Include);

            if (brake == null)
            {
                Debug.LogWarning("[GameplayUiBuilder] No SimpleInput brake button found; the brake was left where it was.");
                return null;
            }

            Reparent(brake.transform, parent);
            RectTransform rect = brake.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(1f, 0f), BrakeAnchoredPosition, new Vector2(BrakeSize, BrakeSize), new Vector2(0.5f, 0.5f));

            return rect;
        }

        // The five car controls, laid on an arc over the brake pedal. Angles run right to
        // left so the indicators end up on the side they signal.
        private static void BuildControlRing(Transform parent, RectTransform brake)
        {
            Vector2 centre = brake != null ? brake.anchoredPosition : BrakeAnchoredPosition;

            GameObject ring = CreateChild(parent, "ControlRing");
            SetRect(ring.GetComponent<RectTransform>(), new Vector2(1f, 0f), centre, new Vector2(2f, 2f), new Vector2(0.5f, 0.5f));

            (Button button, Image icon) left = RingButton(ring.transform, "LeftIndicatorButton", "indicator-left", 162f);
            (Button button, Image icon) headlight = RingButton(ring.transform, "HeadlightButton", "headlight", 126f);
            (Button button, Image icon) camera = RingButton(ring.transform, "CameraButton", "camera", 90f);
            (Button button, Image icon) horn = RingButton(ring.transform, "HornButton", "horn", 54f);
            (Button button, Image icon) right = RingButton(ring.transform, "RightIndicatorButton", "indicator-right", 18f);

            Text cameraMode = CreateLabel(camera.button.transform, "ModeCaption", "FOLLOW", 18, TextAnchor.MiddleCenter);
            cameraMode.color = new Color(1f, 1f, 1f, 0.75f);
            SetRect(cameraMode.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -16f), new Vector2(180f, 24f), new Vector2(0.5f, 0.5f));

            var buttons = parent.gameObject.AddComponent<VehicleControlButtons>();
            SetPrivate(buttons, "headlightButton", headlight.button);
            SetPrivate(buttons, "hornButton", horn.button);
            SetPrivate(buttons, "leftIndicatorButton", left.button);
            SetPrivate(buttons, "rightIndicatorButton", right.button);
            SetPrivate(buttons, "cameraButton", camera.button);
            SetPrivate(buttons, "headlightIcon", headlight.icon);
            SetPrivate(buttons, "leftIndicatorIcon", left.icon);
            SetPrivate(buttons, "rightIndicatorIcon", right.icon);
            SetPrivate(buttons, "cameraModeLabel", cameraMode);
        }

        private static (Button, Image) RingButton(Transform ring, string name, string iconName, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            var offset = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * RingRadius;

            GameObject target = CreateChild(ring, name);
            SetRect(target.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), offset, Vector2.one * RingButtonSize, new Vector2(0.5f, 0.5f));

            Image background = target.AddComponent<Image>();
            background.sprite = IconSpriteGenerator.Load("disc");
            background.color = new Color(0.05f, 0.07f, 0.11f, 0.78f);

            var button = target.AddComponent<Button>();
            button.targetGraphic = background;

            Image icon = CreateIcon(target.transform, "Icon", iconName, IconIdle, RingButtonSize * 0.52f);
            SetRect(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (RingButtonSize * 0.52f), new Vector2(0.5f, 0.5f));

            return (button, icon);
        }

        private static void RetireLegacyControlCanvas()
        {
            GameObject legacy = GameObject.Find("MobileControls");

            if (legacy == null)
            {
                return;
            }

            legacy.SetActive(false);
            EditorUtility.SetDirty(legacy);
        }

        // ================= menu =================================================================

        private static GameObject BuildMenu(Transform parent, GameSession session, out MenuController menu)
        {
            GameObject root = CreateChild(parent, "MenuRoot");
            Stretch(root);

            GameObject home = BuildHomeScreen(root.transform, out Button play, out Button mode, out Button garage, out Button settings, out Button quit, out Text coins);
            GameObject modeScreen = BuildModeScreen(root.transform, out Button practiceMode, out Button challengeMode, out Button freeMode, out Button modeBack);
            GameObject practiceScreen = BuildPracticeScreen(root.transform, out Button practiceBack);
            GameObject garageScreen = BuildGarageScreen(root.transform, out Button garageBack);
            GameObject settingsScreen = BuildSettingsScreen(root.transform, out Button settingsBack);

            menu = root.AddComponent<MenuController>();
            SetPrivate(menu, "session", session);
            SetPrivate(menu, "homeScreen", home);
            SetPrivate(menu, "modeScreen", modeScreen);
            SetPrivate(menu, "practiceScreen", practiceScreen);
            SetPrivate(menu, "garageScreen", garageScreen);
            SetPrivate(menu, "settingsScreen", settingsScreen);
            SetPrivate(menu, "showroom", session.GetComponent<ShowroomCameraRig>());
            SetPrivate(menu, "playButton", play);
            SetPrivate(menu, "modeButton", mode);
            SetPrivate(menu, "garageButton", garage);
            SetPrivate(menu, "settingsButton", settings);
            SetPrivate(menu, "quitButton", quit);
            SetPrivate(menu, "coinLabel", coins);
            SetPrivate(menu, "practiceModeButton", practiceMode);
            SetPrivate(menu, "challengeModeButton", challengeMode);
            SetPrivate(menu, "freeRoamModeButton", freeMode);
            SetPrivateArray(menu, "backButtons", new Object[] { modeBack, practiceBack, garageBack, settingsBack });

            // MenuController would sort this out on its first frame anyway; doing it here
            // keeps the saved scene showing one screen instead of five stacked up.
            modeScreen.SetActive(false);
            practiceScreen.SetActive(false);
            garageScreen.SetActive(false);
            settingsScreen.SetActive(false);

            return root;
        }

        private static GameObject BuildHomeScreen(
            Transform parent,
            out Button play,
            out Button mode,
            out Button garage,
            out Button settings,
            out Button quit,
            out Text coins)
        {
            GameObject screen = CreateChild(parent, "HomeScreen");
            Stretch(screen);

            // A column down the left, not a full-screen sheet: the showroom car stands on
            // the right and the old opaque backdrop is exactly what hid it.
            Image scrim = CreateImage(screen.transform, "Scrim", Scrim);
            StretchVertical(scrim.rectTransform, 0f, 660f);

            Text title = CreateLabel(screen.transform, "Title", "PARKING GAME", 58, TextAnchor.MiddleLeft);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(64f, -176f), new Vector2(560f, 70f), new Vector2(0f, 0.5f));

            Text subtitle = CreateLabel(screen.transform, "Subtitle", "Park it clean", 26, TextAnchor.MiddleLeft);
            subtitle.color = new Color(1f, 1f, 1f, 0.55f);
            SetRect(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(66f, -232f), new Vector2(560f, 36f), new Vector2(0f, 0.5f));

            GameObject chip = CreatePanel(screen.transform, "CoinChip", new Vector2(1f, 1f), new Vector2(-150f, -80f), new Vector2(220f, 72f), HudGlass);
            Image coinIcon = CreateIcon(chip.transform, "Icon", "disc", new Color(1f, 0.82f, 0.25f, 1f), 28f);
            SetRect(coinIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(28f, 28f), new Vector2(0.5f, 0.5f));
            coins = CreateLabel(chip.transform, "Coins", "0", 32, TextAnchor.MiddleLeft);
            SetRect(coins.rectTransform, new Vector2(0f, 0.5f), new Vector2(72f, 0f), new Vector2(130f, 40f), new Vector2(0f, 0.5f));

            play = HomeButton(screen.transform, "PlayButton", "play", "PLAY", 236f, AccentColor, 110f);
            mode = HomeButton(screen.transform, "ModeButton", "compass", "GAME MODE", 116f, ButtonColor, 92f);
            garage = HomeButton(screen.transform, "GarageButton", "car", "GARAGE", 12f, ButtonColor, 92f);
            settings = HomeButton(screen.transform, "SettingsButton", "gear", "SETTINGS", -92f, ButtonColor, 92f);
            quit = HomeButton(screen.transform, "QuitButton", "exit", "QUIT", -196f, ButtonColor, 92f);

            return screen;
        }

        private static Button HomeButton(Transform parent, string name, string iconName, string caption, float y, Color color, float height)
        {
            GameObject target = CreateChild(parent, name);
            SetRect(target.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(64f, y), new Vector2(500f, height), new Vector2(0f, 0.5f));

            Image background = target.AddComponent<Image>();
            background.sprite = IconSpriteGenerator.Load("panel");
            background.type = Image.Type.Sliced;
            background.color = color;

            var button = target.AddComponent<Button>();
            button.targetGraphic = background;

            Image icon = CreateIcon(target.transform, "Icon", iconName, Color.white, height * 0.42f);
            SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(46f, 0f), Vector2.one * (height * 0.42f), new Vector2(0.5f, 0.5f));

            Text label = CreateLabel(target.transform, "Label", caption, 32, TextAnchor.MiddleLeft);
            SetRect(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(94f, 0f), new Vector2(380f, height), new Vector2(0f, 0.5f));

            return button;
        }

        private static GameObject BuildModeScreen(
            Transform parent,
            out Button practice,
            out Button challenge,
            out Button free,
            out Button back)
        {
            GameObject screen = CreateChild(parent, "ModeScreen");
            Stretch(screen);

            Image dim = CreateImage(screen.transform, "Dim", Dim);
            Stretch(dim.gameObject);

            Text title = CreateLabel(screen.transform, "Title", "CHOOSE A MODE", 46, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(800f, 60f), new Vector2(0.5f, 0.5f));

            practice = ModeCard(
                screen.transform,
                "PracticeCard",
                "cone",
                "PRACTICE",
                "One stage at a time. Clear a stage to unlock the next.",
                -520f,
                AccentColor);

            challenge = ModeCard(
                screen.transform,
                "ChallengeCard",
                "stopwatch",
                "CHALLENGE",
                "Every bay in the map, back to back, against the clock. An arrow points at the next one.",
                0f,
                WarnColor);

            free = ModeCard(
                screen.transform,
                "FreeRoamCard",
                "compass",
                "FREE DRIVE",
                "No missions, no timer. Just the map.",
                520f,
                new Color(0.3f, 0.75f, 0.45f));

            back = BackButton(screen.transform);
            return screen;
        }

        private static Button ModeCard(Transform parent, string name, string iconName, string title, string description, float x, Color accent)
        {
            GameObject card = CreateChild(parent, name);
            SetRect(card.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(x, -10f), new Vector2(460f, 540f), new Vector2(0.5f, 0.5f));

            Image background = card.AddComponent<Image>();
            background.sprite = IconSpriteGenerator.Load("panel");
            background.type = Image.Type.Sliced;
            background.color = CardColor;

            var button = card.AddComponent<Button>();
            button.targetGraphic = background;

            Image stripe = CreateImage(card.transform, "Accent", accent);
            SetRect(stripe.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(440f, 8f), new Vector2(0.5f, 0.5f));

            Image icon = CreateIcon(card.transform, "Icon", iconName, accent, 140f);
            SetRect(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(140f, 140f), new Vector2(0.5f, 0.5f));

            Text heading = CreateLabel(card.transform, "Title", title, 38, TextAnchor.MiddleCenter);
            SetRect(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -270f), new Vector2(420f, 50f), new Vector2(0.5f, 0.5f));

            Text body = CreateLabel(card.transform, "Description", description, 24, TextAnchor.UpperCenter);
            body.color = new Color(1f, 1f, 1f, 0.68f);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            SetRect(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(380f, 160f), new Vector2(0.5f, 1f));

            return button;
        }

        private static GameObject BuildPracticeScreen(Transform parent, out Button back)
        {
            GameObject screen = CreateChild(parent, "PracticeScreen");
            Stretch(screen);

            Image dim = CreateImage(screen.transform, "Dim", Dim);
            Stretch(dim.gameObject);

            Text title = CreateLabel(screen.transform, "Title", "PRACTICE", 44, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(600f, 56f), new Vector2(0.5f, 0.5f));

            Text coins = CreateLabel(screen.transform, "Coins", "0", 30, TextAnchor.MiddleRight);
            SetRect(coins.rectTransform, new Vector2(1f, 1f), new Vector2(-80f, -80f), new Vector2(240f, 40f), new Vector2(1f, 0.5f));

            // Thirty cards do not fit on a phone screen, so the grid scrolls.
            GameObject viewport = CreateChild(screen.transform, "Viewport");
            SetRect(viewport.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(1480f, 700f), new Vector2(0.5f, 0.5f));
            viewport.AddComponent<RectMask2D>();

            GameObject content = CreateChild(viewport.transform, "Content");
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(0f, 0f);
            contentRect.offsetMax = new Vector2(0f, 0f);

            var layout = content.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(340f, 150f);
            layout.spacing = new Vector2(18f, 18f);
            layout.padding = new RectOffset(16, 16, 8, 16);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 4;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            MissionCardView cardPrefab = CreateMissionCardPrefab();

            var selection = screen.AddComponent<MissionSelectionView>();
            SetPrivate(selection, "catalog", AssetDatabase.LoadAssetAtPath<MissionCatalog>(MissionCatalogPath));
            SetPrivate(selection, "cardPrefab", cardPrefab);
            SetPrivate(selection, "cardContainer", content.transform);
            SetPrivate(selection, "coinLabel", coins);

            back = BackButton(screen.transform);
            return screen;
        }

        private static GameObject BuildGarageScreen(Transform parent, out Button back)
        {
            GameObject screen = CreateChild(parent, "GarageScreen");
            Stretch(screen);

            // No full-screen dim here on purpose: the right of the screen has to stay clear
            // so the showroom car is visible. That is the "no car in the garage" bug.
            Image scrim = CreateImage(screen.transform, "Scrim", Scrim);
            StretchVertical(scrim.rectTransform, 0f, 760f);

            Text title = CreateLabel(screen.transform, "Title", "GARAGE", 44, TextAnchor.MiddleLeft);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(64f, -170f), new Vector2(400f, 56f), new Vector2(0f, 0.5f));

            Text coins = CreateLabel(screen.transform, "Coins", "0", 32, TextAnchor.MiddleRight);
            SetRect(coins.rectTransform, new Vector2(0f, 1f), new Vector2(700f, -170f), new Vector2(240f, 40f), new Vector2(1f, 0.5f));

            Text carName = CreateLabel(screen.transform, "CarName", "-", 40, TextAnchor.MiddleLeft);
            SetRect(carName.rectTransform, new Vector2(0f, 1f), new Vector2(64f, -252f), new Vector2(620f, 52f), new Vector2(0f, 0.5f));

            Text lockedBadge = CreateLabel(screen.transform, "LockedBadge", "LOCKED", 26, TextAnchor.MiddleLeft);
            lockedBadge.color = WarnColor;
            SetRect(lockedBadge.rectTransform, new Vector2(0f, 1f), new Vector2(64f, -300f), new Vector2(300f, 34f), new Vector2(0f, 0.5f));

            Text selectedBadge = CreateLabel(screen.transform, "SelectedBadge", "SELECTED", 26, TextAnchor.MiddleLeft);
            selectedBadge.color = new Color(0.4f, 0.9f, 0.5f);
            SetRect(selectedBadge.rectTransform, new Vector2(0f, 1f), new Vector2(64f, -300f), new Vector2(300f, 34f), new Vector2(0f, 0.5f));

            // GarageView shows whichever of the two applies; only one can ever be right, so
            // the scene is saved with the other one off.
            lockedBadge.gameObject.SetActive(false);

            Text stats = CreateLabel(screen.transform, "Stats", "-", 28, TextAnchor.UpperLeft);
            stats.color = new Color(1f, 1f, 1f, 0.8f);
            SetRect(stats.rectTransform, new Vector2(0f, 1f), new Vector2(64f, -352f), new Vector2(500f, 180f), new Vector2(0f, 1f));

            // Wide, because this line carries "3000 coins - 1800 more needed" as well as a
            // bare price.
            Text price = CreateLabel(screen.transform, "Price", string.Empty, 26, TextAnchor.MiddleLeft);
            SetRect(price.rectTransform, new Vector2(0f, 1f), new Vector2(64f, -560f), new Vector2(640f, 44f), new Vector2(0f, 0.5f));

            Text paintCaption = CreateLabel(screen.transform, "PaintCaption", "PAINT", 24, TextAnchor.MiddleLeft);
            paintCaption.color = new Color(1f, 1f, 1f, 0.55f);
            SetRect(paintCaption.rectTransform, new Vector2(0f, 0f), new Vector2(64f, 400f), new Vector2(300f, 32f), new Vector2(0f, 0.5f));

            var colorButtons = new Object[8];

            for (int i = 0; i < colorButtons.Length; i++)
            {
                GameObject swatch = CreateChild(screen.transform, $"Color{i + 1}");
                SetRect(swatch.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(84f + i * 82f, 340f), Vector2.one * 64f, new Vector2(0.5f, 0.5f));

                Image image = swatch.AddComponent<Image>();
                image.sprite = IconSpriteGenerator.Load("disc");
                image.color = Color.white;

                var swatchButton = swatch.AddComponent<Button>();
                swatchButton.targetGraphic = image;
                colorButtons[i] = swatchButton;
            }

            Button previous = CircleButton(screen.transform, "PreviousButton", "back", new Vector2(0f, 0f), new Vector2(110f, 230f), 96f);
            Button next = CircleButton(screen.transform, "NextButton", "back", new Vector2(0f, 0f), new Vector2(226f, 230f), 96f);

            // The same chevron turned round, rather than a second icon that has to stay
            // in step with it.
            next.transform.GetChild(0).localRotation = Quaternion.Euler(0f, 180f, 0f);

            Button buy = WideButton(screen.transform, "BuyButton", "BUY", new Vector2(0f, 0f), new Vector2(380f, 230f), new Vector2(180f, 84f), AccentColor);
            Button select = WideButton(screen.transform, "SelectButton", "DRIVE", new Vector2(0f, 0f), new Vector2(580f, 230f), new Vector2(200f, 84f), AccentColor);

            var view = screen.AddComponent<GarageView>();
            SetPrivate(view, "garage", Object.FindFirstObjectByType<GarageManager>(FindObjectsInactive.Include));
            SetPrivate(view, "carNameLabel", carName);
            SetPrivate(view, "coinLabel", coins);
            SetPrivate(view, "priceLabel", price);
            SetPrivate(view, "statsLabel", stats);
            SetPrivate(view, "previousButton", previous);
            SetPrivate(view, "nextButton", next);
            SetPrivate(view, "buyButton", buy);
            SetPrivate(view, "selectButton", select);
            SetPrivate(view, "selectButtonLabel", select.transform.Find("Label").GetComponent<Text>());
            SetPrivate(view, "lockedBadge", lockedBadge.gameObject);
            SetPrivate(view, "selectedBadge", selectedBadge.gameObject);
            SetPrivateArray(view, "colorButtons", colorButtons);

            back = BackButton(screen.transform);
            return screen;
        }

        private static GameObject BuildSettingsScreen(Transform parent, out Button back)
        {
            GameObject screen = CreateChild(parent, "SettingsScreen");
            Stretch(screen);

            Image dim = CreateImage(screen.transform, "Dim", Dim);
            Stretch(dim.gameObject);

            GameObject panel = CreatePanel(screen.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1060f, 820f), PanelColor);

            Text title = CreateLabel(panel.transform, "Title", "SETTINGS", 44, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(600f, 56f), new Vector2(0.5f, 0.5f));

            // Rows are laid out from the top on a fixed pitch, which is what stops the
            // reset button from landing on top of the close button again.
            Slider music = CreateSlider(panel.transform, "MusicSlider", "Music", -150f, 0f, 1f);
            Slider sfx = CreateSlider(panel.transform, "SfxSlider", "SFX", -230f, 0f, 1f);
            Slider sensitivity = CreateSlider(panel.transform, "SensitivitySlider", "Steering", -310f, 0.25f, 3f);

            // Caption on its own row, choices on the row beneath it. Putting both on one
            // row is what had "RESET PROGRESS" sitting across "CLOSE" before.
            Text qualityValue = CreateRowCaption(panel.transform, "Graphics", -390f);
            Button low = SmallButton(panel.transform, "LowButton", "LOW", new Vector2(150f, -456f));
            Button medium = SmallButton(panel.transform, "MediumButton", "MEDIUM", new Vector2(340f, -456f));
            Button high = SmallButton(panel.transform, "HighButton", "HIGH", new Vector2(530f, -456f));

            Text fpsValue = CreateRowCaption(panel.transform, "Frame rate", -540f);
            Button fps30 = SmallButton(panel.transform, "Fps30Button", "30 FPS", new Vector2(150f, -606f));
            Button fps60 = SmallButton(panel.transform, "Fps60Button", "60 FPS", new Vector2(340f, -606f));

            Button reset = WideButton(panel.transform, "ResetButton", "RESET PROGRESS", new Vector2(0.5f, 1f), new Vector2(0f, -726f), new Vector2(400f, 76f), WarnColor);

            GameObject confirm = CreatePanel(panel.transform, "ResetConfirm", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 300f), new Color(0.05f, 0.06f, 0.09f, 0.99f));
            Text confirmText = CreateLabel(confirm.transform, "ConfirmText", "Erase all progress?", 32, TextAnchor.MiddleCenter);
            SetRect(confirmText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(620f, 44f), new Vector2(0.5f, 0.5f));
            Button confirmYes = WideButton(confirm.transform, "ConfirmButton", "ERASE", new Vector2(0.5f, 0f), new Vector2(-120f, 80f), new Vector2(200f, 76f), WarnColor);
            Button confirmNo = WideButton(confirm.transform, "CancelButton", "CANCEL", new Vector2(0.5f, 0f), new Vector2(120f, 80f), new Vector2(200f, 76f), ButtonColor);
            confirm.SetActive(false);

            var view = screen.AddComponent<SettingsView>();
            SetPrivate(view, "settings", Object.FindFirstObjectByType<SettingsManager>(FindObjectsInactive.Include));
            SetPrivate(view, "musicSlider", music);
            SetPrivate(view, "sfxSlider", sfx);
            SetPrivate(view, "sensitivitySlider", sensitivity);
            SetPrivate(view, "lowQualityButton", low);
            SetPrivate(view, "mediumQualityButton", medium);
            SetPrivate(view, "highQualityButton", high);
            SetPrivate(view, "frameRate30Button", fps30);
            SetPrivate(view, "frameRate60Button", fps60);
            SetPrivate(view, "qualityLabel", qualityValue);
            SetPrivate(view, "frameRateLabel", fpsValue);
            SetPrivate(view, "resetProgressButton", reset);
            SetPrivate(view, "resetConfirmPanel", confirm);
            SetPrivate(view, "resetConfirmButton", confirmYes);
            SetPrivate(view, "resetCancelButton", confirmNo);

            back = BackButton(screen.transform);
            return screen;
        }

        // The black that a challenge run swaps levels behind. Built last so it is the last
        // child of the canvas, which is what puts it over everything else.
        private static void BuildScreenFade(Transform canvas, GameObject canvasObject)
        {
            Image sheet = CreateImage(canvas, "ScreenFade", new Color(0f, 0f, 0f, 0f));
            Stretch(sheet.gameObject);

            // Nothing in it to press, and it must not swallow presses meant for whatever is
            // underneath it.
            sheet.raycastTarget = false;
            sheet.gameObject.SetActive(false);

            var fade = canvasObject.AddComponent<ScreenFade>();
            SetPrivate(fade, "sheet", sheet);
        }

        // ================= pause and results ====================================================

        private static GameObject BuildPauseScreen(Transform parent, GameSession session, GameObject canvasObject)
        {
            GameObject screen = CreateChild(parent, "PauseScreen");
            Stretch(screen);

            Image dim = CreateImage(screen.transform, "Dim", Dim);
            Stretch(dim.gameObject);

            GameObject panel = CreatePanel(screen.transform, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 664f), PanelColor);

            Text title = CreateLabel(panel.transform, "Title", "PAUSED", 46, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(600f, 56f), new Vector2(0.5f, 0.5f));

            Text modeLabel = CreateLabel(panel.transform, "Mode", string.Empty, 26, TextAnchor.MiddleCenter);
            modeLabel.color = new Color(1f, 1f, 1f, 0.6f);
            SetRect(modeLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -126f), new Vector2(620f, 36f), new Vector2(0.5f, 0.5f));

            Button resume = WideButton(panel.transform, "ResumeButton", "RESUME", new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(480f, 88f), AccentColor);
            Button restart = WideButton(panel.transform, "RestartButton", "RESTART", new Vector2(0.5f, 1f), new Vector2(0f, -312f), new Vector2(480f, 88f), ButtonColor);
            Button settings = WideButton(panel.transform, "SettingsButton", "SETTINGS", new Vector2(0.5f, 1f), new Vector2(0f, -414f), new Vector2(480f, 88f), ButtonColor);
            Button menu = WideButton(panel.transform, "MenuButton", "MAIN MENU", new Vector2(0.5f, 1f), new Vector2(0f, -516f), new Vector2(480f, 88f), ButtonColor);

            // The pause button lives on the canvas, not inside this screen, because it has
            // to be reachable while the screen is hidden.
            Button pauseButton = CircleButton(parent, "PauseButton", "pause", new Vector2(0f, 1f), new Vector2(90f, -84f), 96f);

            var view = canvasObject.AddComponent<PauseMenuView>();
            SetPrivate(view, "session", session);
            SetPrivate(view, "panel", screen);
            SetPrivate(view, "pauseButton", pauseButton);
            SetPrivate(view, "resumeButton", resume);
            SetPrivate(view, "restartButton", restart);
            SetPrivate(view, "menuButton", menu);
            SetPrivate(view, "modeLabel", modeLabel);
            SetPrivate(view, "settingsButton", settings);

            AddVisibility(canvasObject, new Object[] { pauseButton.gameObject }, false);

            screen.SetActive(false);
            return screen;
        }

        private static void BuildResultScreens(Transform parent)
        {
            GameObject host = CreateChild(parent, "MissionResult");
            Stretch(host);

            GameObject complete = CreatePanel(host.transform, "CompletePanel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880f, 700f), PanelColor);
            Text completeTitle = CreateLabel(complete.transform, "Title", "STAGE CLEAR", 48, TextAnchor.MiddleCenter);
            SetRect(completeTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(760f, 60f), new Vector2(0.5f, 0.5f));

            Sprite starSprite = IconSpriteGenerator.Load("star");
            var resultStars = new Object[3];

            for (int i = 0; i < resultStars.Length; i++)
            {
                Image star = CreateImage(complete.transform, $"Star{i + 1}", new Color(1f, 1f, 1f, 0.2f));
                star.sprite = starSprite;
                SetRect(star.rectTransform, new Vector2(0.5f, 1f), new Vector2((i - 1) * 90f, -170f), Vector2.one * 70f, new Vector2(0.5f, 0.5f));
                resultStars[i] = star;
            }

            Text score = CreateStatRow(complete.transform, "Score", "Score", -260f);
            Text time = CreateStatRow(complete.transform, "Time", "Time", -316f);
            Text collisions = CreateStatRow(complete.transform, "Collisions", "Collisions", -372f);
            Text penalty = CreateStatRow(complete.transform, "Penalty", "Time penalty", -428f);
            Text coins = CreateStatRow(complete.transform, "Coins", "Coins earned", -484f);

            Text best = CreateLabel(complete.transform, "NewBest", "NEW BEST", 26, TextAnchor.MiddleCenter);
            best.color = new Color(1f, 0.82f, 0.25f);
            SetRect(best.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -530f), new Vector2(400f, 34f), new Vector2(0.5f, 0.5f));

            Text allDone = CreateLabel(complete.transform, "AllComplete", "All stages complete", 26, TextAnchor.MiddleCenter);
            SetRect(allDone.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -564f), new Vector2(500f, 34f), new Vector2(0.5f, 0.5f));
            allDone.gameObject.SetActive(false);

            Button next = WideButton(complete.transform, "NextButton", "NEXT", new Vector2(0.5f, 0f), new Vector2(230f, 64f), new Vector2(240f, 84f), AccentColor);
            Button replay = WideButton(complete.transform, "ReplayButton", "RETRY", new Vector2(0.5f, 0f), new Vector2(-10f, 64f), new Vector2(220f, 84f), ButtonColor);
            Button menu = WideButton(complete.transform, "MenuButton", "MENU", new Vector2(0.5f, 0f), new Vector2(-250f, 64f), new Vector2(220f, 84f), ButtonColor);

            GameObject failed = CreatePanel(host.transform, "FailedPanel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 480f), PanelColor);
            Text failedTitle = CreateLabel(failed.transform, "Title", "FAILED", 48, TextAnchor.MiddleCenter);
            SetRect(failedTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(700f, 60f), new Vector2(0.5f, 0.5f));

            Text reason = CreateLabel(failed.transform, "Reason", "-", 30, TextAnchor.MiddleCenter);
            SetRect(reason.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(700f, 44f), new Vector2(0.5f, 0.5f));

            Text failScore = CreateStatRow(failed.transform, "Score", "Score", -230f);

            Button retry = WideButton(failed.transform, "RetryButton", "RETRY", new Vector2(0.5f, 0f), new Vector2(130f, 70f), new Vector2(240f, 84f), AccentColor);
            Button failMenu = WideButton(failed.transform, "MenuButton", "MENU", new Vector2(0.5f, 0f), new Vector2(-130f, 70f), new Vector2(240f, 84f), ButtonColor);

            var view = host.AddComponent<MissionResultView>();
            SetPrivate(view, "missionManager", Object.FindFirstObjectByType<MissionManager>(FindObjectsInactive.Include));
            SetPrivate(view, "catalog", AssetDatabase.LoadAssetAtPath<MissionCatalog>(MissionCatalogPath));
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
        }

        private static void WireSessionObjects(GameSession session, GameObject hud, GameObject pause)
        {
            EditorUtility.SetDirty(session);
            EditorUtility.SetDirty(hud);
            EditorUtility.SetDirty(pause);
        }

        // ================= widgets ==============================================================

        private static MissionCardView CreateMissionCardPrefab()
        {
            const string folder = "Assets/GameAssets/Prefabs/UI";

            if (!AssetDatabase.IsValidFolder("Assets/GameAssets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets/GameAssets", "Prefabs");
            }

            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets/GameAssets/Prefabs", "UI");
            }

            var card = new GameObject("MissionCard", typeof(RectTransform), typeof(Image), typeof(Button));
            var background = card.GetComponent<Image>();
            background.sprite = IconSpriteGenerator.Load("panel");
            background.type = Image.Type.Sliced;
            background.color = CardColor;
            card.GetComponent<RectTransform>().sizeDelta = new Vector2(340f, 150f);

            Text number = CreateLabel(card.transform, "Number", "01", 44, TextAnchor.MiddleLeft);
            SetRect(number.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -38f), new Vector2(100f, 54f), new Vector2(0f, 0.5f));

            Text name = CreateLabel(card.transform, "Name", "Mission", 24, TextAnchor.MiddleLeft);
            SetRect(name.rectTransform, new Vector2(0f, 1f), new Vector2(120f, -38f), new Vector2(200f, 40f), new Vector2(0f, 0.5f));

            Text difficulty = CreateLabel(card.transform, "Difficulty", "*", 22, TextAnchor.MiddleLeft);
            difficulty.color = new Color(1f, 1f, 1f, 0.6f);
            SetRect(difficulty.rectTransform, new Vector2(0f, 0f), new Vector2(24f, 34f), new Vector2(140f, 30f), new Vector2(0f, 0.5f));

            Text bestScore = CreateLabel(card.transform, "BestScore", string.Empty, 20, TextAnchor.MiddleRight);
            bestScore.color = new Color(1f, 1f, 1f, 0.6f);
            SetRect(bestScore.rectTransform, new Vector2(1f, 0f), new Vector2(-24f, 34f), new Vector2(180f, 30f), new Vector2(1f, 0.5f));

            Sprite starSprite = IconSpriteGenerator.Load("star");
            var starImages = new Image[3];

            for (int i = 0; i < 3; i++)
            {
                Image star = CreateImage(card.transform, $"Star{i + 1}", new Color(1f, 1f, 1f, 0.2f));
                star.sprite = starSprite;
                SetRect(star.rectTransform, new Vector2(1f, 1f), new Vector2(-24f - (2 - i) * 34f, -40f), Vector2.one * 28f, new Vector2(0.5f, 0.5f));
                starImages[i] = star;
            }

            Image lockedImage = CreateImage(card.transform, "LockedOverlay", new Color(0.02f, 0.03f, 0.05f, 0.8f));
            lockedImage.sprite = IconSpriteGenerator.Load("panel");
            lockedImage.type = Image.Type.Sliced;
            GameObject locked = lockedImage.gameObject;
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

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(card, CardPrefabPath);
            Object.DestroyImmediate(card);

            return saved.GetComponent<MissionCardView>();
        }

        private static Button BackButton(Transform parent)
        {
            Button button = CircleButton(parent, "BackButton", "back", new Vector2(0f, 0f), new Vector2(90f, 90f), 96f);
            return button;
        }

        private static Button CircleButton(Transform parent, string name, string iconName, Vector2 anchor, Vector2 position, float size)
        {
            GameObject target = CreateChild(parent, name);
            SetRect(target.GetComponent<RectTransform>(), anchor, position, Vector2.one * size, new Vector2(0.5f, 0.5f));

            Image background = target.AddComponent<Image>();
            background.sprite = IconSpriteGenerator.Load("disc");
            background.color = new Color(0.05f, 0.07f, 0.11f, 0.8f);

            var button = target.AddComponent<Button>();
            button.targetGraphic = background;

            Image icon = CreateIcon(target.transform, "Icon", iconName, IconIdle, size * 0.5f);
            SetRect(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (size * 0.5f), new Vector2(0.5f, 0.5f));

            return button;
        }

        private static Button WideButton(Transform parent, string name, string caption, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            GameObject target = CreateChild(parent, name);
            SetRect(target.GetComponent<RectTransform>(), anchor, position, size, new Vector2(0.5f, 0.5f));

            Image background = target.AddComponent<Image>();
            background.sprite = IconSpriteGenerator.Load("panel");
            background.type = Image.Type.Sliced;
            background.color = color;

            var button = target.AddComponent<Button>();
            button.targetGraphic = background;

            Text label = CreateLabel(target.transform, "Label", caption, 28, TextAnchor.MiddleCenter);
            Stretch(label.gameObject);

            return button;
        }

        private static Button SmallButton(Transform parent, string name, string caption, Vector2 position)
        {
            return WideButton(parent, name, caption, new Vector2(0f, 1f), position, new Vector2(170f, 68f), ButtonColor);
        }

        // Returns the value label on the right; the caption on the left needs no reference.
        private static Text CreateRowCaption(Transform parent, string caption, float y)
        {
            Text label = CreateLabel(parent, caption + "Caption", caption, 28, TextAnchor.MiddleLeft);
            SetRect(label.rectTransform, new Vector2(0f, 1f), new Vector2(60f, y), new Vector2(320f, 40f), new Vector2(0f, 0.5f));

            Text value = CreateLabel(parent, caption + "Value", "-", 26, TextAnchor.MiddleRight);
            value.color = new Color(1f, 1f, 1f, 0.7f);
            SetRect(value.rectTransform, new Vector2(1f, 1f), new Vector2(-60f, y), new Vector2(320f, 40f), new Vector2(1f, 0.5f));

            return value;
        }

        private static Slider CreateSlider(Transform parent, string name, string caption, float y, float min, float max)
        {
            Text label = CreateLabel(parent, name + "Caption", caption, 28, TextAnchor.MiddleLeft);
            SetRect(label.rectTransform, new Vector2(0f, 1f), new Vector2(60f, y), new Vector2(260f, 40f), new Vector2(0f, 0.5f));

            GameObject sliderObject = CreateChild(parent, name);
            SetRect(sliderObject.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(340f, y), new Vector2(620f, 40f), new Vector2(0f, 0.5f));

            Image background = CreateImage(sliderObject.transform, "Background", new Color(1f, 1f, 1f, 0.15f));
            background.sprite = IconSpriteGenerator.Load("panel");
            background.type = Image.Type.Sliced;
            Stretch(background.gameObject);

            GameObject fillArea = CreateChild(sliderObject.transform, "FillArea");
            Stretch(fillArea);
            Image fill = CreateImage(fillArea.transform, "Fill", AccentColor);
            fill.sprite = IconSpriteGenerator.Load("panel");
            fill.type = Image.Type.Sliced;
            Stretch(fill.gameObject);

            GameObject handleArea = CreateChild(sliderObject.transform, "HandleArea");
            Stretch(handleArea);
            Image handle = CreateImage(handleArea.transform, "Handle", Color.white);
            handle.sprite = IconSpriteGenerator.Load("disc");
            handle.rectTransform.sizeDelta = new Vector2(44f, 44f);

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

        private static Text CreateStatRow(Transform parent, string name, string caption, float y)
        {
            Text label = CreateLabel(parent, name + "Caption", caption, 26, TextAnchor.MiddleLeft);
            label.color = new Color(1f, 1f, 1f, 0.65f);
            SetRect(label.rectTransform, new Vector2(0f, 1f), new Vector2(80f, y), new Vector2(360f, 36f), new Vector2(0f, 0.5f));

            Text value = CreateLabel(parent, name, "-", 30, TextAnchor.MiddleRight);
            SetRect(value.rectTransform, new Vector2(1f, 1f), new Vector2(-80f, y), new Vector2(300f, 36f), new Vector2(1f, 0.5f));

            return value;
        }

        // ================= primitives ===========================================================

        // Parks SimpleInput's widgets at the scene root so that rebuilding the canvas never
        // destroys them. They are only ever here for the length of one build.
        private static void DetachDrivingControls()
        {
            foreach (Component widget in FindDrivingControls())
            {
                if (widget != null)
                {
                    Undo.SetTransformParent(widget.transform, null, "Detach driving control");
                    widget.transform.SetParent(null, false);
                }
            }
        }

        private static IEnumerable<Component> FindDrivingControls()
        {
            yield return Object.FindFirstObjectByType<SteeringWheel>(FindObjectsInactive.Include);
            yield return Object.FindFirstObjectByType<AxisInputUIArrows>(FindObjectsInactive.Include);
            yield return Object.FindFirstObjectByType<ButtonInputUI>(FindObjectsInactive.Include);
        }

        private static void DestroyIfPresent(string objectName)
        {
            GameObject existing = GameObject.Find(objectName);

            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }
        }

        private static void Reparent(Transform target, Transform parent)
        {
            Undo.SetTransformParent(target, parent, "Move driving control");
            target.SetParent(parent, false);
            target.localScale = Vector3.one;
            target.localRotation = Quaternion.identity;
        }

        private static void AddVisibility(GameObject host, Object[] targets, bool visibleInMenu)
        {
            var visibility = host.AddComponent<SessionVisibility>();
            SetPrivateArray(visibility, "targets", targets);
            SetPrivate(visibility, "visibleInMenu", visibleInMenu);
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

        // Full height, fixed width, pinned to one side: the menu's side panels.
        private static void StretchVertical(RectTransform rect, float left, float width)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);

            // With the anchors collapsed on x, the offsets are the panel's left and right
            // edges; setting sizeDelta afterwards would undo them.
            rect.offsetMin = new Vector2(left, 0f);
            rect.offsetMax = new Vector2(left + width, 0f);
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            GameObject panel = CreateChild(parent, name);

            var image = panel.AddComponent<Image>();
            image.sprite = IconSpriteGenerator.Load("panel");
            image.type = Image.Type.Sliced;
            image.color = color;

            SetRect(panel.GetComponent<RectTransform>(), anchor, position, size, new Vector2(0.5f, 0.5f));
            return panel;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject target = CreateChild(parent, name);
            var image = target.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static Image CreateIcon(Transform parent, string name, string iconName, Color color, float size)
        {
            GameObject target = CreateChild(parent, name);
            var image = target.AddComponent<Image>();

            image.sprite = IconSpriteGenerator.Load(iconName);
            image.color = color;
            image.raycastTarget = false;
            image.preserveAspect = true;

            if (image.sprite == null)
            {
                Debug.LogWarning($"[GameplayUiBuilder] No icon called '{iconName}'; run Generate UI Icons.");
            }

            SetRect(image.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size, new Vector2(0.5f, 0.5f));
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

            SetRect(label.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 40f), new Vector2(0.5f, 0.5f));
            return label;
        }

        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
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

        private static void SetPrivate(Object target, string fieldName, float value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogWarning($"[GameplayUiBuilder] No field '{fieldName}' on {target.GetType().Name}.");
                return;
            }

            property.floatValue = value;
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

        // ----- scene lookups --------------------------------------------------------------------

        private static GameObject FindMenuCamera()
        {
            Camera main = Camera.main;

            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (camera != main && camera.GetComponent<CarCameraController>() == null)
                {
                    return camera.gameObject;
                }
            }

            Debug.LogWarning("[GameplayUiBuilder] No separate menu camera found; the menu will use the gameplay camera.");
            return null;
        }

        private static GameObject FindPlayerCarContainer()
        {
            CarController[] cars = Object.FindObjectsByType<CarController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return cars.Length > 0 && cars[0].transform.parent != null ? cars[0].transform.parent.gameObject : null;
        }

        // The showroom cars are the ones with no CarController: same models, no physics.
        private static GameObject FindShowroomContainer()
        {
            var garage = Object.FindFirstObjectByType<GarageManager>(FindObjectsInactive.Include);

            if (garage != null && garage.ShowroomCarContainer != null)
            {
                return garage.ShowroomCarContainer;
            }

            GameObject playerCars = FindPlayerCarContainer();

            foreach (CarSelection selection in Object.FindObjectsByType<CarSelection>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (selection.allCarsContainer != null && selection.allCarsContainer != playerCars)
                {
                    return selection.allCarsContainer;
                }
            }

            Debug.LogWarning("[GameplayUiBuilder] No showroom car container found; the garage will show no car.");
            return null;
        }
    }
}
