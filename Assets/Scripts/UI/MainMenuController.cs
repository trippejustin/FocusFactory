using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private Transform robot;
    [SerializeField] private RobotVisualController robotVisuals;
    [SerializeField] private Button startButton;

    [SerializeField] private float thinkingDuration = 1f;
    [SerializeField] private float driveSpeed = 2f;
    [SerializeField] private float driveDuration = 3f;

    public void StartStudy()
    {
        startButton.interactable = false;
        StartCoroutine(DriveAway());
    }

    private IEnumerator DriveAway()
    {
        robotVisuals.StartThinking();

        yield return new WaitForSecondsRealtime(thinkingDuration);

        robotVisuals.StopThinking();
        robotVisuals.SetMovementDirection(Vector2.right);

        float elapsed = 0f;

        while (elapsed < driveDuration)
        {
            robot.position += Vector3.right *
                driveSpeed * Time.unscaledDeltaTime;

            elapsed += Time.unscaledDeltaTime;

            yield return null;
        }

        SceneTransition.Instance.LoadScene("TrialSelection");
    }
}