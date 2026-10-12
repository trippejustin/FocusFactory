using System.Collections;
using TMPro;
using UnityEngine;

public class AgentStatusDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text statusText;

    [Header("Timing")]
    [SerializeField] private float minimumVisibleDuration = 2.5f;
    [SerializeField] private float secondsPerCharacter = 0.06f;
    [SerializeField] private float maximumVisibleDuration = 8f;
    [SerializeField] private float fadeDuration = 1f;

    private Coroutine fadeCoroutine;

    public void SetStatus(string message)
    {
        if (statusText == null)
            return;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        statusText.text = message;
        statusText.alpha = 1f;

        float visibleDuration = Mathf.Clamp(
            message.Length * secondsPerCharacter,
            minimumVisibleDuration,
            maximumVisibleDuration
        );

        fadeCoroutine = StartCoroutine(
            FadeStatus(visibleDuration)
        );
    }

    public void ClearStatus()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        if (statusText == null)
            return;

        statusText.text = "";
        statusText.alpha = 0f;
    }

    private IEnumerator FadeStatus(float visibleDuration)
    {
        yield return new WaitForSeconds(visibleDuration);

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            statusText.alpha = Mathf.Lerp(
                1f,
                0f,
                elapsed / fadeDuration
            );

            yield return null;
        }

        statusText.alpha = 0f;
        fadeCoroutine = null;
    }
}