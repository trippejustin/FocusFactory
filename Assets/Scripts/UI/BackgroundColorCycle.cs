using UnityEngine;

[RequireComponent(typeof(Camera))]
public class BackgroundColorCycle : MonoBehaviour
{
    [SerializeField] private float cycleDuration = 30f;

    private Color charcoal = new Color32(41, 39, 36, 255);
    private Color forest = new Color32(41, 56, 50, 255);
    private Color graphite = new Color32(53, 56, 61, 255);

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Update()
    {
        float t = Time.unscaledTime * Mathf.PI * 2f
                  / cycleDuration;

        float a = (Mathf.Cos(t) + 1f) / 2f;
        float b = (Mathf.Cos(t - 2.094395f) + 1f) / 2f;
        float c = (Mathf.Cos(t - 4.188790f) + 1f) / 2f;

        cam.backgroundColor =
            (charcoal * a + forest * b + graphite * c)
            / (a + b + c);
    }
}