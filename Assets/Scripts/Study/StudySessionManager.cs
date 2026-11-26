using System.Collections.Generic;
using UnityEngine;

public class StudySessionManager : MonoBehaviour
{
    public static StudySessionManager Instance { get; private set; }

    public enum ComplexityLevel
    {
        Low,
        Medium,
        High
    }

    public enum InterfaceStyle
    {
        None,
        Spatial,
        Conventional
    }

    [System.Serializable]
    public class TrialResult
    {
        public ComplexityLevel complexity;
        public InterfaceStyle interfaceStyle;
        public float completionTime;
        public bool completed;
    }

    [Header("Current Study State")]
    [SerializeField] private ComplexityLevel currentComplexity =
        ComplexityLevel.Low;

    [SerializeField] private InterfaceStyle currentInterface =
        InterfaceStyle.None;

    [SerializeField] private int currentRun = 0;

    [Header("Interface Choices")]
    [SerializeField] private InterfaceStyle lowFirstChoice =
        InterfaceStyle.None;

    [SerializeField] private InterfaceStyle mediumFirstChoice =
        InterfaceStyle.None;

    [SerializeField] private InterfaceStyle highPreferredChoice =
        InterfaceStyle.None;

    [Header("Completed Trials")]
    [SerializeField] private List<TrialResult> trialResults =
        new List<TrialResult>();

    public ComplexityLevel CurrentComplexity => currentComplexity;
    public InterfaceStyle CurrentInterface => currentInterface;
    public int CurrentRun => currentRun;

    public IReadOnlyList<TrialResult> TrialResults => trialResults;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    public void BeginStudy()
    {
        currentRun = 0;
        currentComplexity = ComplexityLevel.Low;
        currentInterface = InterfaceStyle.None;

        lowFirstChoice = InterfaceStyle.None;
        mediumFirstChoice = InterfaceStyle.None;
        highPreferredChoice = InterfaceStyle.None;

        trialResults.Clear();

        StudyDataLogger.BeginNewStudy();
        Debug.Log("Study started.");
    }

    public void ChooseInterface(InterfaceStyle choice)
    {
        if (choice == InterfaceStyle.None)
            return;

        // Run 0:
        // Low complexity, participant chooses first interface.
        if (currentRun == 0)
        {
            lowFirstChoice = choice;
            currentInterface = choice;
        }

        // Run 2:
        // Medium complexity, participant chooses first interface.
        else if (currentRun == 2)
        {
            mediumFirstChoice = choice;
            currentInterface = choice;
        }

        // Run 4:
        // High complexity, participant chooses first interface.
        else if (currentRun == 4)
        {
            highPreferredChoice = choice;
            currentInterface = choice;
        }

        Debug.Log(
            $"Selected interface: {currentInterface}"
        );
    }

    public void CompleteCurrentTrial(float completionTime)
    {
        TrialResult result = new TrialResult
        {
            complexity = currentComplexity,
            interfaceStyle = currentInterface,
            completionTime = completionTime,
            completed = true
        };

        trialResults.Add(result);

        StudyDataLogger.RecordTrialCompleted(completionTime);

        Debug.Log(
            $"Completed Run {currentRun + 1}: " +
            $"{currentComplexity}, {currentInterface}"
        );

        AdvanceStudy();
    }

    private void AdvanceStudy()
    {
        currentRun++;

        switch (currentRun)
        {
            // Low complexity, second interface.
            case 1:
                currentComplexity = ComplexityLevel.Low;
                currentInterface = GetOtherInterface(lowFirstChoice);
                break;

            // Medium complexity, choose first interface.
            case 2:
                currentComplexity = ComplexityLevel.Medium;
                currentInterface = InterfaceStyle.None;
                break;

            // Medium complexity, second interface.
            case 3:
                currentComplexity = ComplexityLevel.Medium;
                currentInterface = GetOtherInterface(mediumFirstChoice);
                break;

            // High complexity, choose first interface.
            case 4:
                currentComplexity = ComplexityLevel.High;
                currentInterface = InterfaceStyle.None;
                break;

            // High complexity, second interface.
            case 5:
                currentComplexity = ComplexityLevel.High;
                currentInterface = GetOtherInterface(highPreferredChoice);
                break;

            // Study complete.
            case 6:
                currentInterface = InterfaceStyle.None;

                Debug.Log("STUDY COMPLETE");
                break;
        }
    }

    private InterfaceStyle GetOtherInterface(
        InterfaceStyle interfaceStyle)
    {
        if (interfaceStyle == InterfaceStyle.Spatial)
            return InterfaceStyle.Conventional;

        if (interfaceStyle == InterfaceStyle.Conventional)
            return InterfaceStyle.Spatial;

        return InterfaceStyle.None;
    }

    public bool IsStudyComplete()
    {
        return currentRun >= 6;
    }
}