using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class HazardRegion : MonoBehaviour
{
    public enum HazardMode
    {
        Disabled,
        Permanent,
        Cycling
    }

    [Header("Hazard Settings")]
    [SerializeField] private HazardMode mode = HazardMode.Disabled;
    [SerializeField, Min(0.1f)] private float safeDuration = 15f;
    [SerializeField, Min(0.1f)] private float hazardousDuration = 15f;
    [SerializeField] private bool startHazardous = false;

    [Header("Exposure")]
    [SerializeField] private Collider2D hazardArea;
    [SerializeField] private bool contaminateGroundBatteries;

    [Header("Optional Visuals")]
    [SerializeField] private GameObject hazardVisuals;
    [SerializeField] private Animator reactorAnimator;
    [SerializeField] private ParticleSystem hazardParticles;
    [SerializeField] private TMP_Text hazardStatusText;
    [SerializeField] private GameObject warningLight;

    [Header("Hazard Status Colors")]
    [SerializeField] private Color safeStatusColor = new Color32(101, 223, 133, 255);
    [SerializeField] private Color warningStatusColor = new Color32(242, 211, 78, 255);
    [SerializeField] private Color hazardousStatusColor = new Color32(255, 85, 85, 255);

    public bool IsHazardous { get; private set; }
    public float TimeRemaining { get; private set; }
    public event Action<bool> OnHazardChanged;

    private static readonly HashSet<HazardRegion> activeRegions = new();
    private RobotInteraction robotInteraction;
    private double cycleStart;
    private HazardMode previousMode;

    private const float PuddleFadeDuration = 0.5f;
    private readonly Dictionary<SpriteRenderer, float> puddleAlphas = new();
    private float puddleOpacity;

    private void Awake()
    {
        if (hazardVisuals == null)
            return;

        foreach (SpriteRenderer sprite in hazardVisuals.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (hazardParticles != null && sprite.transform.IsChildOf(hazardParticles.transform))
                continue;

            if (warningLight != null && sprite.transform.IsChildOf(warningLight.transform))
                continue;

            puddleAlphas[sprite] = sprite.color.a;
        }
    }

    private void SetPuddleOpacity(float opacity)
    {
        puddleOpacity = opacity;

        foreach (KeyValuePair<SpriteRenderer, float> puddle in puddleAlphas)
        {
            if (puddle.Key == null)
                continue;

            Color color = puddle.Key.color;
            color.a = puddle.Value * opacity;
            puddle.Key.color = color;
        }
    }

    private void UpdatePuddleFade()
    {
        float targetOpacity = IsHazardous ? 1f : 0f;

        if (puddleOpacity != targetOpacity)
        {
            SetPuddleOpacity(Mathf.MoveTowards(puddleOpacity, targetOpacity,
                Time.deltaTime / PuddleFadeDuration));
        }

        if (!IsHazardous && puddleOpacity <= 0f && hazardVisuals != null && hazardVisuals.activeSelf)
            hazardVisuals.SetActive(false);
    }

    private void OnEnable()
    {
        activeRegions.Add(this);

        if (hazardArea == null)
            hazardArea = GetComponent<Collider2D>();

        robotInteraction = FindFirstObjectByType<RobotInteraction>();

        if (hazardArea == null)
            Debug.LogWarning("HazardRegion: Assign the room's region collider.", this);

        SetPuddleOpacity(0f);
        ResetCycle();
    }

    private void FixedUpdate()
    {
        CheckLocalExposure(robotInteraction);
    }

    private void Update()
    {
        if (mode != previousMode)
            ResetCycle();
        else
            UpdateHazard();

        UpdatePuddleFade();

        if (robotInteraction == null)
            robotInteraction = FindFirstObjectByType<RobotInteraction>();

        CheckLocalExposure(robotInteraction);
        CheckGroundBatteryExposure();
    }

    public static void CheckExposure(RobotInteraction robot)
    {
        foreach (HazardRegion region in activeRegions)
        {
            if (region != null)
                region.CheckLocalExposure(robot);
        }
    }

    private void CheckLocalExposure(RobotInteraction robot)
    {
        if (!IsHazardous || hazardArea == null || !hazardArea.enabled ||
            !hazardArea.gameObject.activeInHierarchy || robot == null)
            return;

        if (!hazardArea.OverlapPoint(robot.transform.position))
            return;

        robot.Contaminate();
    }

    private void CheckGroundBatteryExposure()
    {
        if (!contaminateGroundBatteries || !IsHazardous || hazardArea == null ||
            !hazardArea.enabled || !hazardArea.gameObject.activeInHierarchy)
            return;

        foreach (BatteryContamination battery in FindObjectsByType<BatteryContamination>(FindObjectsSortMode.None))
        {
            if (battery.IsContaminated ||
                battery.GetComponentInParent<RobotInteraction>() != null ||
                (robotInteraction != null && robotInteraction.CarriedObject != null &&
                 robotInteraction.CarriedObject.gameObject == battery.gameObject))
                continue;

            if (hazardArea.OverlapPoint(battery.transform.position))
                battery.Contaminate();
        }
    }

    public void SetMode(HazardMode newMode)
    {
        mode = newMode;
        if (isActiveAndEnabled)
            ResetCycle();
    }

    public void ResetCycle()
    {
        cycleStart = Time.timeAsDouble;
        previousMode = mode;
        UpdateHazard(true);
    }

    private void UpdateHazard(bool forceVisuals = false)
    {
        bool hazardous = mode == HazardMode.Permanent;
        TimeRemaining = 0f;

        if (mode == HazardMode.Cycling)
        {
            float safe = Mathf.Max(0.1f, safeDuration);
            float active = Mathf.Max(0.1f, hazardousDuration);
            double cycleLength = (double)safe + active;
            double elapsed = Time.timeAsDouble - cycleStart;
            double phase = (elapsed + (startHazardous ? safe : 0f)) % cycleLength;

            hazardous = phase >= safe;
            TimeRemaining = (float)(hazardous ? cycleLength - phase : safe - phase);
        }

        bool changed = IsHazardous != hazardous;
        IsHazardous = hazardous;

        if (changed || forceVisuals)
        {
            if (IsHazardous && hazardVisuals != null)
                hazardVisuals.SetActive(true);

            if (reactorAnimator != null)
                reactorAnimator.speed = IsHazardous ? 1f : 0f;

            if (hazardParticles != null)
            {
                if (IsHazardous)
                    hazardParticles.Play();
                else if (hazardParticles.gameObject.activeInHierarchy)
                    hazardParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (warningLight != null)
                warningLight.SetActive(IsHazardous);

            // Keep the parent active until the puddles finish fading out.
            if (!IsHazardous && hazardVisuals != null &&
                (puddleOpacity <= 0f || puddleAlphas.Count == 0))
                hazardVisuals.SetActive(false);
        }

        if (hazardStatusText != null)
        {
            hazardStatusText.color = IsHazardous ? hazardousStatusColor :
                (mode == HazardMode.Cycling ? warningStatusColor : safeStatusColor);

            if (mode == HazardMode.Disabled)
                hazardStatusText.text = "SAFE";
            else if (mode == HazardMode.Permanent)
                hazardStatusText.text = "HAZARDOUS";
            else
                hazardStatusText.text = (IsHazardous ? "SAFE IN " : "HAZARD IN ")
                    + Mathf.CeilToInt(TimeRemaining) + "s";
        }

        if (changed || forceVisuals)
            CheckGroundBatteryExposure();

        if (changed)
            OnHazardChanged?.Invoke(IsHazardous);
    }

    private void OnDisable()
    {
        activeRegions.Remove(this);

        if (hazardParticles != null && hazardParticles.gameObject.activeInHierarchy)
            hazardParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        SetPuddleOpacity(0f);

        if (hazardVisuals != null)
            hazardVisuals.SetActive(false);

        if (reactorAnimator != null)
            reactorAnimator.speed = 0f;

        if (warningLight != null)
            warningLight.SetActive(false);

        bool wasHazardous = IsHazardous;
        IsHazardous = false;
        TimeRemaining = 0f;
        if (wasHazardous)
            OnHazardChanged?.Invoke(false);
    }
}
