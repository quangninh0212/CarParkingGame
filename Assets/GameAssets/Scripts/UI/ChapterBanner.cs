using System.Collections;
using CarParkingGame.Missions;
using CarParkingGame.Story;
using UnityEngine;
using UnityEngine.UI;

namespace CarParkingGame.UI
{
    // The chapter title, shown across the screen for a few seconds when a level opens one.
    //
    // It does not pause the game and it does not take input. A card the player has to
    // dismiss would be in the way by the third time they replayed the level; a banner that
    // fades itself out is read once and ignored afterwards, which is what a chapter title
    // is for.
    public class ChapterBanner : MonoBehaviour
    {
        [SerializeField] private MissionManager missions;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text lineLabel;

        [SerializeField] private float fadeSeconds = 0.5f;
        [SerializeField] private float holdSeconds = 3.6f;

        private Coroutine running;

        private MissionManager Missions => missions != null ? missions : MissionManager.Instance;

        private void OnEnable()
        {
            MissionManager runner = Missions;

            if (runner != null)
            {
                runner.MissionStarted += OnMissionStarted;
            }

            Hide();
        }

        private void OnDisable()
        {
            MissionManager runner = Missions;

            if (runner != null)
            {
                runner.MissionStarted -= OnMissionStarted;
            }
        }

        private void OnMissionStarted(MissionDefinition definition)
        {
            if (definition == null || !StoryLibrary.Opens(definition.MissionId, out StoryLibrary.Chapter chapter))
            {
                Hide();
                return;
            }

            if (titleLabel != null)
            {
                titleLabel.text = $"CHAPTER {chapter.number}  -  {chapter.title}";
            }

            if (lineLabel != null)
            {
                lineLabel.text = chapter.line;
            }

            if (running != null)
            {
                StopCoroutine(running);
            }

            running = StartCoroutine(Show());
        }

        private IEnumerator Show()
        {
            yield return Ramp(0f, 1f);
            yield return new WaitForSecondsRealtime(holdSeconds);
            yield return Ramp(1f, 0f);

            Hide();
            running = null;
        }

        // Unscaled, because a level can start with the clock stopped and a banner waiting
        // on a stopped clock never finishes.
        private IEnumerator Ramp(float from, float to)
        {
            if (group == null)
            {
                yield break;
            }

            group.gameObject.SetActive(true);

            for (float elapsed = 0f; elapsed < fadeSeconds; elapsed += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, elapsed / fadeSeconds);
                yield return null;
            }

            group.alpha = to;
        }

        private void Hide()
        {
            if (group == null)
            {
                return;
            }

            group.alpha = 0f;
            group.gameObject.SetActive(false);
        }
    }
}
