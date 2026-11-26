using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance { get; private set; }

    [SerializeField] private CanvasGroup fadeOverlay;
    [SerializeField] private float fadeDuration = 0.35f;

    private bool transitioning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        fadeOverlay.alpha = 0f;
        fadeOverlay.blocksRaycasts = false;
    }

    public void LoadScene(string sceneName)
    {
        if (!transitioning)
            StartCoroutine(Transition(sceneName));
    }

    private IEnumerator Transition(string sceneName)
    {
        transitioning = true;
        fadeOverlay.blocksRaycasts = true;

        yield return Fade(1f);
        yield return SceneManager.LoadSceneAsync(sceneName);
        yield return Fade(0f);

        fadeOverlay.blocksRaycasts = false;
        transitioning = false;
    }

    private IEnumerator Fade(float target)
    {
        float start = fadeOverlay.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeOverlay.alpha = Mathf.Lerp(
                start, target, elapsed / fadeDuration);

            yield return null;
        }

        fadeOverlay.alpha = target;
    }
}