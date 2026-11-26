using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(WorldObject))]
public class BatteryContamination : MonoBehaviour
{
    [SerializeField] private bool startContaminated;
    [SerializeField] private GameObject contaminatedIndicator;

    [SerializeField] private ParticleSystem contaminationParticles;

    public bool IsContaminated { get; private set; }

    private void Awake()
    {
        SetContaminated(startContaminated);
    }

    public void Contaminate()
    {
        if (IsContaminated)
            return;

        SetContaminated(true);

        WorldObject worldObject = GetComponent<WorldObject>();
        StudyDataLogger.RecordBatteryContaminated(
            worldObject != null ? worldObject.ObjectId : gameObject.name
        );

        Debug.Log("Battery contaminated. Decontamination required.");
    }

    public void Clean()
    {
        SetContaminated(false);
    }

    private void SetContaminated(bool contaminated)
    {
        IsContaminated = contaminated;

        if (contaminatedIndicator != null)
            contaminatedIndicator.SetActive(contaminated);

        if (contaminationParticles != null && contaminationParticles.gameObject.activeInHierarchy)
        {
            if (contaminated) contaminationParticles.Play(true);
            else contaminationParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
