using System;
using System.Collections.Generic;
using System.IO;
using CarParkingGame.Garage;
using CarParkingGame.Progression;
using CarParkingGame.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CarParkingGame.EditorTools
{
    // Renders each UI screen to a PNG so the layout can be looked at without a device.
    //
    // A screen-space overlay canvas cannot be captured, so the canvas is switched to
    // screen-space camera for the length of the capture and pointed at the menu camera -
    // which also means the shots include the showroom car standing behind the UI, and so
    // show whether the garage panel is covering it.
    //
    // It never saves the scene. Everything it changes is in memory and thrown away when
    // the Editor exits.
    public static class UiScreenshotTool
    {
        private const string ScenePath = "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity";

        public static void CaptureFromCommandLine()
        {
            string directory = GetArg("-shotDir") ?? "ui-shots";
            int width = int.TryParse(GetArg("-shotWidth"), out int w) ? w : 2400;
            int height = int.TryParse(GetArg("-shotHeight"), out int h) ? h : 1080;

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError($"[UiScreenshotTool] Could not open '{ScenePath}'.");
                EditorApplication.Exit(1);
                return;
            }

            Directory.CreateDirectory(directory);

            GameObject canvasObject = GameObject.Find("GameUI");

            if (canvasObject == null)
            {
                Debug.LogError("[UiScreenshotTool] No 'GameUI' canvas; run Build Game UI first.");
                EditorApplication.Exit(1);
                return;
            }

            var canvas = canvasObject.GetComponent<Canvas>();
            Camera camera = PrepareCamera(width, height);

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;

            ShowroomCameraRig rig = PrepareShowroom();
            PaintShowroomCars();

            Transform menuRoot = canvasObject.transform.Find("MenuRoot");
            Transform hud = canvasObject.transform.Find("HUD");

            SetActive(hud, false);
            SetActive(menuRoot, true);

            foreach (KeyValuePair<string, ShowroomFocus> screen in Screens())
            {
                ShowOnly(menuRoot, screen.Key);
                rig?.FrameImmediately(screen.Value);
                Capture(camera, width, height, Path.Combine(directory, screen.Key + ".png"));
            }

            // The HUD, over the same backdrop, with the mission readouts filled in so the
            // panels are not all empty rectangles.
            SetActive(menuRoot, false);
            SetActive(hud, true);
            PopulateHudSamples(hud);
            rig?.FrameImmediately(ShowroomFocus.Home);
            Capture(camera, width, height, Path.Combine(directory, "Hud.png"));

            Debug.Log($"[UiScreenshotTool] Wrote screen captures to '{directory}' at {width}x{height}.");
            EditorApplication.Exit(0);
        }

        // The showroom cars are painted a colour nothing on them already is, so the
        // garage shot shows which parts of a car the garage actually repaints. Painting
        // the whole car - glass, lamps, tyres and all - is a fault a player has reported
        // once already, and it is invisible in a shot of a car left its own colour.
        private static void PaintShowroomCars()
        {
            var sample = new Color(0.16f, 0.42f, 0.95f);
            int painted = 0;

            foreach (CarPaintTarget paint in UnityEngine.Object.FindObjectsByType<CarPaintTarget>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!paint.HasTargets)
                {
                    Debug.LogWarning($"[UiScreenshotTool] '{paint.name}' has no paint slots.", paint);
                    continue;
                }

                paint.Apply(sample);
                painted++;
            }

            Debug.Log($"[UiScreenshotTool] Painted {painted} car(s) for the shot.");
        }

        private static IEnumerable<KeyValuePair<string, ShowroomFocus>> Screens()
        {
            yield return new KeyValuePair<string, ShowroomFocus>("HomeScreen", ShowroomFocus.Home);
            yield return new KeyValuePair<string, ShowroomFocus>("ModeScreen", ShowroomFocus.Home);
            yield return new KeyValuePair<string, ShowroomFocus>("PracticeScreen", ShowroomFocus.Home);
            yield return new KeyValuePair<string, ShowroomFocus>("GarageScreen", ShowroomFocus.Garage);
            yield return new KeyValuePair<string, ShowroomFocus>("SettingsScreen", ShowroomFocus.Home);
        }

        private static Camera PrepareCamera(int width, int height)
        {
            var rig = UnityEngine.Object.FindFirstObjectByType<ShowroomCameraRig>(FindObjectsInactive.Include);
            Camera camera = null;

            foreach (Camera candidate in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.GetComponent<CarCameraController>() == null)
                {
                    camera = candidate;
                    break;
                }
            }

            if (camera == null)
            {
                camera = Camera.main;
            }

            camera.gameObject.SetActive(true);
            camera.enabled = true;
            camera.aspect = width / (float)height;

            return camera;
        }

        private static ShowroomCameraRig PrepareShowroom()
        {
            var garage = UnityEngine.Object.FindFirstObjectByType<GarageManager>(FindObjectsInactive.Include);

            // GarageManager only picks a car in play mode, so one is shown by hand here.
            if (garage != null && garage.ShowroomCarContainer != null)
            {
                Transform container = garage.ShowroomCarContainer.transform;
                container.gameObject.SetActive(true);

                for (int i = 0; i < container.childCount; i++)
                {
                    container.GetChild(i).gameObject.SetActive(i == 0);
                }
            }

            return UnityEngine.Object.FindFirstObjectByType<ShowroomCameraRig>(FindObjectsInactive.Include);
        }

        private static void ShowOnly(Transform menuRoot, string screenName)
        {
            if (menuRoot == null)
            {
                return;
            }

            for (int i = 0; i < menuRoot.childCount; i++)
            {
                Transform child = menuRoot.GetChild(i);
                child.gameObject.SetActive(child.name == screenName);
            }

            if (screenName == "PracticeScreen")
            {
                FillMissionGrid(menuRoot.Find(screenName));
            }
        }

        // The mission cards are built at runtime, so the capture gets a handful of stand-in
        // cards; it is the grid's spacing and the scroll area being checked, not the data.
        private static void FillMissionGrid(Transform practiceScreen)
        {
            if (practiceScreen == null)
            {
                return;
            }

            Transform content = practiceScreen.Find("Viewport/Content");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/Prefabs/UI/MissionCard.prefab");

            if (content == null || prefab == null || content.childCount > 0)
            {
                return;
            }

            for (int i = 0; i < 12; i++)
            {
                GameObject card = UnityEngine.Object.Instantiate(prefab, content);
                card.name = $"SampleCard{i}";

                SetText(card.transform.Find("Number"), (i + 1).ToString("00"));
                SetText(card.transform.Find("Name"), "Stage " + (i + 1));
                SetText(card.transform.Find("Difficulty"), new string('*', 1 + i % 5));
                SetText(card.transform.Find("BestScore"), i < 4 ? "Best 92" : string.Empty);

                Transform locked = card.transform.Find("LockedOverlay");

                if (locked != null)
                {
                    // Asked the same way the real card asks, so the shot does not show a
                    // row of padlocks the game will not show.
                    locked.gameObject.SetActive(!MissionUnlocking.IsOpen(i + 1, null));
                }
            }
        }

        private static void PopulateHudSamples(Transform hud)
        {
            if (hud == null)
            {
                return;
            }

            SetText(hud.Find("InfoPanel/MissionName"), "STAGE 3/8  Advanced Bay");
            SetText(hud.Find("InfoPanel/Score"), "92");
            SetText(hud.Find("InfoPanel/Coins"), "1200");
            SetText(hud.Find("TimerChip/Timer"), "1:24");

            // The mirror only renders while a car is being driven, which does not happen in
            // the editor, so the shot shows the empty glass. It is here to check that the
            // frame sits where a driver would look for it and does not cover the HUD.
            Transform mirrors = hud.Find("RearMirrors");

            if (mirrors != null)
            {
                mirrors.gameObject.SetActive(true);

                foreach (RawImage glass in mirrors.GetComponentsInChildren<RawImage>(true))
                {
                    glass.color = new Color(0.16f, 0.17f, 0.19f, 1f);
                }
            }

            // The bay counter is off until a level says it wants more than one, so the
            // shot has to switch it on to show what it looks like when one does.
            Transform bayChip = hud.Find("BayChip");

            if (bayChip != null)
            {
                bayChip.gameObject.SetActive(true);
                SetText(bayChip.Find("BayCount"), "1/3");
            }

            SetText(hud.Find("SpeedPanel/Speed"), "18");
            SetText(hud.Find("ParkingFeedback/Hint"), "Straighten up");

            Transform bar = hud.Find("ParkingFeedback/ProgressTrack/ProgressBar");

            if (bar != null && bar.TryGetComponent(out Image fill))
            {
                fill.fillAmount = 0.6f;
            }

            Transform arrow = hud.Find("GuideArrow/Arrow");

            if (arrow != null)
            {
                arrow.gameObject.SetActive(true);
                ((RectTransform)arrow).anchoredPosition = new Vector2(380f, 240f);
                SetText(arrow.Find("Distance"), "64 m");
            }
        }

        private static void SetText(Transform target, string value)
        {
            if (target != null && target.TryGetComponent(out Text label))
            {
                label.text = value;
            }
        }

        private static void SetActive(Transform target, bool active)
        {
            if (target != null)
            {
                target.gameObject.SetActive(active);
            }
        }

        private static void Capture(Camera camera, int width, int height, string path)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1
            };

            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;

            camera.targetTexture = target;

            Canvas.ForceUpdateCanvases();
            camera.Render();

            RenderTexture.active = target;

            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();

            File.WriteAllBytes(path, image.EncodeToPNG());

            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;

            UnityEngine.Object.DestroyImmediate(image);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }

        private static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
