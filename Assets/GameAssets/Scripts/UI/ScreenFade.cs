using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CarParkingGame.UI
{
    // A black sheet over everything, for the moment a level is swapped out from under the
    // player.
    //
    // Challenge mode used to leave every level standing and expect the player to drive
    // from one bay to the next. That stopped working when the levels became walled car
    // parks half a kilometre off the circuit: there is no road between them, and there is
    // not meant to be one. The car is set down at the next level instead, and this covers
    // the cut so it reads as a scene change rather than as the world jumping.
    public class ScreenFade : MonoBehaviour
    {
        [SerializeField] private Image sheet;
        [SerializeField] private float fadeSeconds = 0.35f;
        [SerializeField] private float holdSeconds = 0.15f;

        public static ScreenFade Instance { get; private set; }

        private Coroutine running;

        private void Awake()
        {
            Instance = this;
            SetAlpha(0f);

            if (sheet != null)
            {
                sheet.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // Fades out, does the swap behind the black, fades back in.
        //
        // The swap runs whether or not there is a sheet to fade. A missing reference is
        // allowed to cost the transition; it is not allowed to cost the player the level.
        public void Cover(Action swap)
        {
            if (sheet == null || !isActiveAndEnabled)
            {
                swap?.Invoke();
                return;
            }

            if (running != null)
            {
                StopCoroutine(running);
            }

            running = StartCoroutine(Run(swap));
        }

        private IEnumerator Run(Action swap)
        {
            sheet.gameObject.SetActive(true);

            yield return Ramp(0f, 1f);

            swap?.Invoke();

            yield return new WaitForSecondsRealtime(holdSeconds);

            yield return Ramp(1f, 0f);

            sheet.gameObject.SetActive(false);
            running = null;
        }

        // Unscaled time throughout: a mission ending stops the clock, and a fade waiting
        // on a stopped clock never finishes.
        private IEnumerator Ramp(float from, float to)
        {
            for (float elapsed = 0f; elapsed < fadeSeconds; elapsed += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(from, to, elapsed / fadeSeconds));
                yield return null;
            }

            SetAlpha(to);
        }

        private void SetAlpha(float alpha)
        {
            if (sheet == null)
            {
                return;
            }

            Color colour = sheet.color;
            colour.a = alpha;
            sheet.color = colour;
        }
    }
}
