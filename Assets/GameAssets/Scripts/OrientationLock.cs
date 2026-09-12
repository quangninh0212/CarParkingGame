using UnityEngine;

/// <summary>
/// Forces the game into landscape at runtime, no matter which scene loads first.
/// The player settings already restrict the Android manifest to sensorLandscape;
/// this is the safety net for devices/launchers that ignore it.
/// </summary>
public static class OrientationLock
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.AutoRotation;
    }
}
