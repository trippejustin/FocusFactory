using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class OllamaClient : MonoBehaviour
{
    [Header("Ollama")]
    [SerializeField] private string endpoint =
        "http://localhost:11434/api/chat";

    [SerializeField] private string model =
        "gpt-oss:20b";

    [Header("Performance")]
    [SerializeField] private int maxResponseTokens = 256;
    [SerializeField] private string keepAlive = "30m";

    [Header("Retries")]
    [SerializeField] private int maxUnreadableRetries = 2;

    public event Action OnRequestStarted;
    public event Action OnRequestFinished;

    public string Model
    {
        get => model;
        set => model = value;
    }

    public bool HasActiveRequest =>
        activeCoroutine != null;

    private Coroutine activeCoroutine;
    private UnityWebRequest activeRequest;

    public void SendPrompt(
        string prompt,
        Action<string> onSuccess,
        Action<string> onError)
    {
        OnRequestStarted?.Invoke();

        activeCoroutine =
            StartCoroutine(
                SendPromptCoroutine(
                    prompt,
                    onSuccess,
                    onError,
                    0
                )
            );
    }

    public void CancelCurrentRequest()
    {
        if (activeCoroutine == null &&
            activeRequest == null)
        {
            return;
        }

        if (activeRequest != null)
        {
            activeRequest.Abort();
            activeRequest.Dispose();
            activeRequest = null;
        }

        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }

        Debug.Log(
            "OllamaClient: Current request cancelled."
        );

        OnRequestFinished?.Invoke();
    }

    private IEnumerator SendPromptCoroutine(
        string prompt,
        Action<string> onSuccess,
        Action<string> onError,
        int unreadableRetryCount)
    {
        string requestPrompt = prompt;

        if (unreadableRetryCount > 0)
        {
            requestPrompt +=
                "\n\nIMPORTANT: Your previous response could not be read. " +
                "Try again. Return exactly one valid JSON object and nothing else.";
        }

        OllamaRequest requestBody =
            new OllamaRequest
            {
                model = model,
                stream = false,
                format = "json",
                think = "low",
                keep_alive = keepAlive,

                options = new OllamaOptions
                {
                    num_predict = maxResponseTokens
                },

                messages = new[]
                {
                    new OllamaMessage
                    {
                        role = "user",
                        content = requestPrompt
                    }
                }
            };

        string json =
            JsonUtility.ToJson(
                requestBody
            );

        activeRequest =
            new UnityWebRequest(
                endpoint,
                UnityWebRequest.kHttpVerbPOST
            );

        byte[] body =
            System.Text.Encoding.UTF8.GetBytes(
                json
            );

        activeRequest.uploadHandler =
            new UploadHandlerRaw(body);

        activeRequest.downloadHandler =
            new DownloadHandlerBuffer();

        activeRequest.SetRequestHeader(
            "Content-Type",
            "application/json"
        );

        yield return activeRequest.SendWebRequest();

        UnityWebRequest completedRequest =
            activeRequest;

        activeRequest = null;

        if (completedRequest.result !=
            UnityWebRequest.Result.Success)
        {
            string error =
                $"Ollama request failed: " +
                $"{completedRequest.error}\n" +
                $"{completedRequest.downloadHandler.text}";

            Debug.LogError(error);

            completedRequest.Dispose();

            activeCoroutine = null;

            OnRequestFinished?.Invoke();

            onError?.Invoke(error);

            yield break;
        }

        OllamaResponse response = null;

        try
        {
            response =
                JsonUtility.FromJson<OllamaResponse>(
                    completedRequest.downloadHandler.text
                );
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"Ollama response parsing failed: " +
                $"{exception.Message}"
            );
        }

        if (response != null)
        {
            Debug.Log(
                $"Ollama timing | " +
                $"Total: {NanosecondsToSeconds(response.total_duration):F2}s | " +
                $"Load: {NanosecondsToSeconds(response.load_duration):F2}s | " +
                $"Prompt: {NanosecondsToSeconds(response.prompt_eval_duration):F2}s | " +
                $"Generation: {NanosecondsToSeconds(response.eval_duration):F2}s | " +
                $"Tokens: {response.eval_count} | " +
                $"Stop: {response.done_reason}"
            );
        }

        if (response == null ||
            response.message == null ||
            string.IsNullOrWhiteSpace(
                response.message.content))
        {
            string rawResponse =
                completedRequest.downloadHandler.text;

            completedRequest.Dispose();

            if (unreadableRetryCount <
                maxUnreadableRetries)
            {
                int nextRetry =
                    unreadableRetryCount + 1;

                Debug.LogWarning(
                    $"Ollama returned an unreadable response. " +
                    $"Retrying {nextRetry}/{maxUnreadableRetries}."
                );

                yield return SendPromptCoroutine(
                    prompt,
                    onSuccess,
                    onError,
                    nextRetry
                );

                yield break;
            }

            string error =
                "Ollama repeatedly returned unreadable responses:\n" +
                rawResponse;

            Debug.LogError(error);

            activeCoroutine = null;

            OnRequestFinished?.Invoke();

            onError?.Invoke(error);

            yield break;
        }

        string responseContent =
            response.message.content;

        completedRequest.Dispose();

        activeCoroutine = null;

        OnRequestFinished?.Invoke();

        onSuccess?.Invoke(
            responseContent
        );
    }

    private double NanosecondsToSeconds(
        long nanoseconds)
    {
        return nanoseconds /
               1_000_000_000.0;
    }

    [Serializable]
    private class OllamaRequest
    {
        public string model;
        public OllamaMessage[] messages;
        public bool stream;
        public string format;
        public string think;
        public string keep_alive;
        public OllamaOptions options;
    }

    [Serializable]
    private class OllamaOptions
    {
        public int num_predict;
    }

    [Serializable]
    private class OllamaMessage
    {
        public string role;
        public string content;
        public string thinking;
    }

    [Serializable]
    private class OllamaResponse
    {
        public OllamaMessage message;

        public bool done;
        public string done_reason;

        public long total_duration;
        public long load_duration;
        public long prompt_eval_duration;
        public long eval_duration;

        public int prompt_eval_count;
        public int prompt_eval_cached_count;
        public int eval_count;
    }
}