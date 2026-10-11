using UnityEngine;
using UnityEngine.InputSystem;

public class OllamaConnectionTest : MonoBehaviour
{
    [SerializeField] private OllamaClient ollamaClient;

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            TestConnection();
        }
    }

    private void TestConnection()
    {
        const string prompt =
            "Reply with valid JSON only: {\"status\":\"ok\",\"message\":\"Ollama connection successful\"}";

        Debug.Log("Sending test request to Ollama...");

        ollamaClient.SendPrompt(
            prompt,
            response => Debug.Log($"Ollama response: {response}"),
            error => Debug.LogError($"Ollama error: {error}")
        );
    }
}
