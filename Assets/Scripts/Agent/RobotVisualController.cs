using System.Collections;
using UnityEngine;

public class RobotVisualController : MonoBehaviour
{
    private enum FacingDirection
    {
        Front,
        Back,
        Left,
        Right
    }

    [Header("References")]
    [SerializeField] private SpriteRenderer robotSpriteRenderer;
    [SerializeField] private OllamaClient ollamaClient;

    [Header("Down / Front")]
    [SerializeField] private Sprite frontSprite;
    [SerializeField] private Sprite frontBlinkSprite;

    [Header("Up / Back")]
    [SerializeField] private Sprite backSprite;

    [Header("Left")]
    [SerializeField] private Sprite leftSprite;
    [SerializeField] private Sprite leftBlinkSprite;

    [Header("Right")]
    [SerializeField] private Sprite rightSprite;
    [SerializeField] private Sprite rightBlinkSprite;

    [Header("Thinking")]
    [SerializeField] private Sprite thinkingFrame0;
    [SerializeField] private Sprite thinkingFrame1;
    [SerializeField] private Sprite thinkingFrame2;
    [SerializeField] private Sprite thinkingFrame3;
    [SerializeField] private float thinkingFrameDuration = 0.1f;

    [Header("Blinking")]
    [SerializeField] private float minimumBlinkDelay = 2.5f;
    [SerializeField] private float maximumBlinkDelay = 6f;
    [SerializeField] private float blinkDuration = 0.12f;

    private FacingDirection currentDirection =
        FacingDirection.Front;

    private bool isThinking;
    private bool isBlinking;

    private int activeOllamaRequests;

    private Coroutine blinkRoutine;
    private Coroutine thinkingRoutine;
    private Coroutine thinkingEndRoutine;

    private void Awake()
    {
        if (robotSpriteRenderer == null)
        {
            robotSpriteRenderer =
                GetComponent<SpriteRenderer>();
        }

        ApplyDirectionalSprite();
    }

    private void OnEnable()
    {
        if (ollamaClient != null)
        {
            ollamaClient.OnRequestStarted +=
                HandleOllamaRequestStarted;

            ollamaClient.OnRequestFinished +=
                HandleOllamaRequestFinished;
        }

        blinkRoutine =
            StartCoroutine(BlinkRoutine());
    }

    private void OnDisable()
    {
        if (ollamaClient != null)
        {
            ollamaClient.OnRequestStarted -=
                HandleOllamaRequestStarted;

            ollamaClient.OnRequestFinished -=
                HandleOllamaRequestFinished;
        }

        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        if (thinkingRoutine != null)
        {
            StopCoroutine(thinkingRoutine);
            thinkingRoutine = null;
        }

        if (thinkingEndRoutine != null)
        {
            StopCoroutine(thinkingEndRoutine);
            thinkingEndRoutine = null;
        }
    }

    public void SetMovementDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        if (Mathf.Abs(direction.x) >
            Mathf.Abs(direction.y))
        {
            currentDirection =
                direction.x > 0f
                    ? FacingDirection.Right
                    : FacingDirection.Left;
        }
        else
        {
            currentDirection =
                direction.y > 0f
                    ? FacingDirection.Back
                    : FacingDirection.Front;
        }

        if (!isThinking)
        {
            ApplyDirectionalSprite();
        }
    }

    private IEnumerator BlinkRoutine()
    {
        while (true)
        {
            float delay =
                Random.Range(
                    minimumBlinkDelay,
                    maximumBlinkDelay
                );

            yield return new WaitForSeconds(delay);

            if (isThinking)
                continue;

            if (currentDirection ==
                FacingDirection.Back)
            {
                continue;
            }

            isBlinking = true;
            ApplyDirectionalSprite();

            yield return new WaitForSeconds(
                blinkDuration
            );

            isBlinking = false;

            if (!isThinking)
            {
                ApplyDirectionalSprite();
            }
        }
    }

    private IEnumerator ThinkingRoutine()
    {
        Sprite[] frames =
        {
            thinkingFrame0,
            thinkingFrame1,
            thinkingFrame2,
            thinkingFrame3
        };

        int frameIndex = 0;

        while (isThinking)
        {
            Sprite frame =
                frames[frameIndex];

            if (frame != null &&
                robotSpriteRenderer != null)
            {
                robotSpriteRenderer.sprite =
                    frame;
            }

            frameIndex =
                (frameIndex + 1) %
                frames.Length;

            yield return new WaitForSeconds(
                thinkingFrameDuration
            );
        }
    }

    private void HandleOllamaRequestStarted()
    {
        activeOllamaRequests++;

        if (thinkingEndRoutine != null)
        {
            StopCoroutine(
                thinkingEndRoutine
            );

            thinkingEndRoutine = null;
        }

        StartThinking();
    }

    private void HandleOllamaRequestFinished()
    {
        activeOllamaRequests =
            Mathf.Max(
                0,
                activeOllamaRequests - 1
            );

        if (thinkingEndRoutine != null)
        {
            StopCoroutine(
                thinkingEndRoutine
            );
        }

        thinkingEndRoutine =
            StartCoroutine(
                FinishThinkingNextFrame()
            );
    }

    private IEnumerator FinishThinkingNextFrame()
    {
        yield return null;

        if (activeOllamaRequests == 0)
        {
            StopThinking();
        }

        thinkingEndRoutine = null;
    }

    public void StartThinking()
    {
        if (isThinking)
            return;

        isThinking = true;
        isBlinking = false;

        if (thinkingRoutine != null)
        {
            StopCoroutine(
                thinkingRoutine
            );
        }

        thinkingRoutine =
            StartCoroutine(
                ThinkingRoutine()
            );
    }

    public void StopThinking()
    {
        if (!isThinking)
            return;

        isThinking = false;

        if (thinkingRoutine != null)
        {
            StopCoroutine(
                thinkingRoutine
            );

            thinkingRoutine = null;
        }

        ApplyDirectionalSprite();
    }

    private void ApplyDirectionalSprite()
    {
        if (robotSpriteRenderer == null)
            return;

        switch (currentDirection)
        {
            case FacingDirection.Front:
                robotSpriteRenderer.sprite =
                    isBlinking &&
                    frontBlinkSprite != null
                        ? frontBlinkSprite
                        : frontSprite;
                break;

            case FacingDirection.Back:
                robotSpriteRenderer.sprite =
                    backSprite;
                break;

            case FacingDirection.Left:
                robotSpriteRenderer.sprite =
                    isBlinking &&
                    leftBlinkSprite != null
                        ? leftBlinkSprite
                        : leftSprite;
                break;

            case FacingDirection.Right:
                robotSpriteRenderer.sprite =
                    isBlinking &&
                    rightBlinkSprite != null
                        ? rightBlinkSprite
                        : rightSprite;
                break;
        }
    }
    
    public void ResetToDefaultFacing()
    {
        currentDirection = FacingDirection.Front;
        isBlinking = false;

        if (!isThinking)
        {
            ApplyDirectionalSprite();
        }
    }
}