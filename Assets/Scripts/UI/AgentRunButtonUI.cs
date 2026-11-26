using UnityEngine;
using UnityEngine.UI;

public class AgentRunButtonUI : MonoBehaviour
{
    [SerializeField] private AgentBrain agentBrain;
    [SerializeField] private Button button;
    [SerializeField] private Image buttonImage;

    [Header("Button Sprites")]
    [SerializeField] private Sprite playSprite;
    [SerializeField] private Sprite pauseSprite;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (buttonImage == null)
            buttonImage = GetComponent<Image>();

        if (button == null)
        {
            Debug.LogError(
                "AgentRunButtonUI: No Button component assigned or found."
            );

            return;
        }

        if (buttonImage == null)
        {
            Debug.LogError(
                "AgentRunButtonUI: No Image component assigned or found."
            );

            return;
        }

        button.onClick.AddListener(HandleClick);

        RefreshVisual();
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }

    private void Update()
    {
        RefreshVisual();
    }

    private void HandleClick()
    {
        if (agentBrain == null)
        {
            Debug.LogError(
                "AgentRunButtonUI: AgentBrain is not assigned."
            );

            return;
        }

        if (agentBrain.IsAgentRunning)
        {
            agentBrain.StopAgent();
            StudyDataLogger.RecordAgentPaused();
        }
        else
        {
            bool hadStarted = agentBrain.TaskHasStarted;
            agentBrain.StartAgent();

            if (agentBrain.IsAgentRunning)
            {
                if (hadStarted)
                    StudyDataLogger.RecordAgentResumed();
                else
                    StudyDataLogger.RecordEvent("AgentStarted");
            }
        }

        RefreshVisual();
    }

    private void RefreshVisual()
    {
        if (agentBrain == null ||
            buttonImage == null)
        {
            return;
        }

        buttonImage.sprite =
            agentBrain.IsAgentRunning
                ? pauseSprite
                : playSprite;
    }
}