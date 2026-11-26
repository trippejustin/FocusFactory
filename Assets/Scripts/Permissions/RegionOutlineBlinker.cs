using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PermissionRegion))]
public class RegionOutlineBlinker : MonoBehaviour
{
    [Header("Blink")]
    [SerializeField] private float blinkSpeed = 4f;
    [SerializeField] private float minAlpha = 0.15f;
    [SerializeField] private float maxAlpha = 0.75f;

    private PermissionRegion region;
    private Coroutine blinkRoutine;
    private bool presentationEnabled = true;

    private void Awake()
    {
        region =
            GetComponent<PermissionRegion>();
    }

    public void SetPresentationEnabled(bool enabled)
    {
        presentationEnabled = enabled;

        if (!enabled)
        {
            StopBlinking();
        }
    }

    public void StartBlinking()
    {
        if (!presentationEnabled ||
            region == null)
        {
            return;
        }

        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
        }

        blinkRoutine =
            StartCoroutine(BlinkRoutine());
    }

    public void StopBlinking()
    {
        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        if (region != null)
        {
            region.ClearBlinkAlphaOverride();
        }
    }

    private IEnumerator BlinkRoutine()
    {
        while (true)
        {
            float t =
                Mathf.PingPong(
                    Time.time * blinkSpeed,
                    1f
                );

            float alpha =
                Mathf.Lerp(
                    minAlpha,
                    maxAlpha,
                    t
                );

            region.SetBlinkAlphaOverride(
                alpha
            );

            yield return null;
        }
    }
}
