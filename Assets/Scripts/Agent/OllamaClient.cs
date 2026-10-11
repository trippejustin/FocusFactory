using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class OllamaClient : MonoBehaviour
{
    [Header("Ollama")]
    [SerializeField] private string endpoint = "http://localhost:11434/api/chat";
    [SerializeField] private string model = "qwen3:8b";

    public string Model
    {
        get => model;
        set => model = value;
    }

    public void SendPrompt(
        string prompt,
        Action<string> onSuccess,
        Action<string> onError)
    {
        StartCoroutine(SendPromptCoroutine(prompt, onSuccess, onError));
    }

    private IEnumerator SendPromptCoroutine(
        string prompt,
        Action<string> onSuccess,
        Action<string> onError)
    {
        OllamaRequest requestBody = new OllamaRequest
        {
            model = model,
            stream = false,
            format = "json",
            messages = new[]
            {
                new OllamaMessage
                {
                    role = "user",
                    content = prompt
                }
            }
        };

        string json = JsonUtility.ToJson(requestBody);

        using UnityWebRequest request =
            new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST);

        byte[] body = System.Text.Encoding.UTF8.GetBytes(json);

        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            string error =
                $"Ollama request failed: {request.error}\n{request.downloadHandler.text}";

            Debug.LogError(error);
            onError?.Invoke(error);
            yield break;
        }

        OllamaResponse response =
            JsonUtility.FromJson<OllamaResponse>(request.downloadHandler.text);

        if (response == null ||
            response.message == null ||
            string.IsNullOrWhiteSpace(response.message.content))
        {
            string error =
                $"Ollama returned an unreadable response:\n{request.downloadHandler.text}";

            Debug.LogError(error);
            onError?.Invoke(error);
            yield break;
        }

        onSuccess?.Invoke(response.message.content);
    }

    [Serializable]
    private class OllamaRequest
    {
        public string model;
        public OllamaMessage[] messages;
        public bool stream;
        public string format;
    }

    [Serializable]
    private class OllamaMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    private class OllamaResponse
    {
        public OllamaMessage message;
        public bool done;
    }
}
