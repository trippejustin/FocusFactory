using TMPro;
using UnityEngine;

public class AgentStatusDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text statusText;

    public void SetStatus(string message)
    {
        if (statusText == null)
            return;

        statusText.text = message;
    }

    public void ClearStatus()
    {
        if (statusText == null)
            return;

        statusText.text = "";
    }
}