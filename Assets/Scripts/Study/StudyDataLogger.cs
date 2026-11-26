using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StudyDataLogger : MonoBehaviour
{
    private static StudyDataLogger instance;

    private string sessionId;
    private string participantId;
    private string eventsPath;
    private string trialsPath;

    private string trialScene = "";
    private int trialRun;
    private int trialAttempt;
    private StudySessionManager.ComplexityLevel trialComplexity;
    private StudySessionManager.InterfaceStyle trialInterface;
    private double trialStartRealtime;
    private bool trialActive;
    private bool trialCompleted;

    private PermissionManager boundPermissionManager;
    private TaskManager boundTaskManager;
    private readonly Dictionary<int, int> attemptsByRun = new();

    private int permissionChanges;
    private int allowSelections;
    private int askSelections;
    private int avoidSelections;
    private int askRequests;
    private int askApprovals;
    private int askDenials;
    private int routeBlocks;
    private int robotContaminations;
    private int batteryContaminations;
    private int decontaminations;
    private int actionFailures;
    private int pauses;
    private int resumes;
    private int needsHelpEvents;

    public static string SessionId => instance != null ? instance.sessionId : "";
    public static string ParticipantId => instance != null ? instance.participantId : "";
    public static string EventsFilePath => instance != null ? instance.eventsPath : "";
    public static string TrialsFilePath => instance != null ? instance.trialsPath : "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateLogger()
    {
        if (instance != null)
            return;

        GameObject loggerObject = new GameObject("StudyDataLogger");
        instance = loggerObject.AddComponent<StudyDataLogger>();
        DontDestroyOnLoad(loggerObject);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        participantId = Environment.GetEnvironmentVariable("FOCUSFACTORY_PARTICIPANT_ID");
        if (string.IsNullOrWhiteSpace(participantId))
            participantId = PlayerPrefs.GetString("StudyParticipantId", "Anonymous");
        StartNewSessionFiles();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        PermissionRegion.OnPermissionChanged += HandlePermissionChanged;
    }

    private void OnApplicationQuit()
    {
        if (trialActive)
        {
            WriteEvent("TrialAbandoned", "", "Application quit");
            FinalizeTrial(false, CurrentTrialElapsed());
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        PermissionRegion.OnPermissionChanged -= HandlePermissionChanged;
        UnbindSceneObjects();
    }

    public static void BeginNewStudy()
    {
        if (instance == null)
            return;

        if (instance.trialActive && !instance.trialCompleted)
            instance.FinalizeTrial(false, instance.CurrentTrialElapsed());

        instance.attemptsByRun.Clear();
        instance.StartNewSessionFiles();
        RecordEvent("StudyStarted");
    }

    public static void SetParticipantId(string id)
    {
        if (instance == null)
            return;

        string cleaned = string.IsNullOrWhiteSpace(id) ? "Anonymous" : id.Trim();
        instance.participantId = cleaned;
        PlayerPrefs.SetString("StudyParticipantId", cleaned);
        PlayerPrefs.Save();
        RecordEvent("ParticipantIdSet", cleaned);
    }

    public static void RecordEvent(string eventType, string subject = "", string details = "")
    {
        if (instance == null)
            return;

        instance.WriteEvent(eventType, subject, details);
    }

    public static void RecordAskRequested(string regionName)
    {
        if (instance == null) return;
        instance.askRequests++;
        instance.WriteEvent("AskRequested", regionName);
    }

    public static void RecordAskApproved(string regionName)
    {
        if (instance == null) return;
        instance.askApprovals++;
        instance.WriteEvent("AskApproved", regionName);
    }

    public static void RecordAskDenied(string regionName)
    {
        if (instance == null) return;
        instance.askDenials++;
        instance.WriteEvent("AskDenied", regionName);
    }

    public static void RecordRouteBlocked(string regionName)
    {
        if (instance == null) return;
        instance.routeBlocks++;
        instance.WriteEvent("RouteBlocked", regionName);
    }

    public static void RecordRobotContaminated()
    {
        if (instance == null) return;
        instance.robotContaminations++;
        instance.WriteEvent("RobotContaminated");
    }

    public static void RecordBatteryContaminated(string batteryId)
    {
        if (instance == null) return;
        instance.batteryContaminations++;
        instance.WriteEvent("BatteryContaminated", batteryId);
    }

    public static void RecordDecontamination(string subject)
    {
        if (instance == null) return;
        instance.decontaminations++;
        instance.WriteEvent("Decontaminated", subject);
    }

    public static void RecordActionFailure(string action, string target, string reason)
    {
        if (instance == null) return;
        instance.actionFailures++;
        instance.WriteEvent("ActionFailed", action, string.IsNullOrWhiteSpace(target) ? reason : target + " | " + reason);
    }

    public static void RecordAgentPaused()
    {
        if (instance == null) return;
        instance.pauses++;
        instance.WriteEvent("AgentPaused");
    }

    public static void RecordAgentResumed()
    {
        if (instance == null) return;
        instance.resumes++;
        instance.WriteEvent("AgentResumed");
    }

    public static void RecordNeedsHelp(string reason)
    {
        if (instance == null) return;
        instance.needsHelpEvents++;
        instance.WriteEvent("NeedsHelp", "", reason);
    }

    public static void RecordTrialCompleted(float completionTime)
    {
        if (instance == null)
            return;

        instance.WriteEvent("TrialCompleted", "", completionTime.ToString("F3", CultureInfo.InvariantCulture));
        instance.FinalizeTrial(true, completionTime);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UnbindSceneObjects();

        if (trialActive && !trialCompleted && scene.name != trialScene)
            FinalizeTrial(false, CurrentTrialElapsed());

        if (IsTrialScene(scene.name))
            BeginTrial(scene.name);

        BindSceneObjects();
    }

    private bool IsTrialScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        return sceneName.StartsWith("Trial", StringComparison.OrdinalIgnoreCase) &&
               !sceneName.Equals("TrialSelection", StringComparison.OrdinalIgnoreCase);
    }

    private void BeginTrial(string sceneName)
    {
        StudySessionManager study = StudySessionManager.Instance;

        trialScene = sceneName;
        trialRun = study != null ? study.CurrentRun : -1;
        trialComplexity = study != null ? study.CurrentComplexity : StudySessionManager.ComplexityLevel.Low;
        trialInterface = study != null ? study.CurrentInterface : StudySessionManager.InterfaceStyle.None;
        trialStartRealtime = Time.realtimeSinceStartupAsDouble;
        trialActive = true;
        trialCompleted = false;

        if (!attemptsByRun.ContainsKey(trialRun))
            attemptsByRun[trialRun] = 0;
        attemptsByRun[trialRun]++;
        trialAttempt = attemptsByRun[trialRun];

        ResetTrialCounters();
        WriteEvent("TrialLoaded");
    }

    private void BindSceneObjects()
    {
        boundPermissionManager = FindFirstObjectByType<PermissionManager>();
        boundTaskManager = FindFirstObjectByType<TaskManager>();

        if (boundTaskManager != null)
        {
            boundTaskManager.OnTaskStarted += HandleTaskStarted;
            boundTaskManager.OnTaskCompleted += HandleTaskCompleted;
            boundTaskManager.OnStepCompleted += HandleStepCompleted;
        }

    }

    private void UnbindSceneObjects()
    {
        if (boundTaskManager != null)
        {
            boundTaskManager.OnTaskStarted -= HandleTaskStarted;
            boundTaskManager.OnTaskCompleted -= HandleTaskCompleted;
            boundTaskManager.OnStepCompleted -= HandleStepCompleted;
        }

        boundTaskManager = null;
        boundPermissionManager = null;
    }

    private void HandleTaskStarted(TaskDefinition task)
    {
        WriteEvent("TaskStarted", task != null ? task.TaskId : "");
    }

    private void HandleTaskCompleted(TaskDefinition task)
    {
        WriteEvent("TaskObjectiveCompleted", task != null ? task.TaskId : "");
    }

    private void HandleStepCompleted(TaskManager.TaskStep step)
    {
        WriteEvent("TaskStepCompleted", step != null ? step.id : "", step != null ? step.description : "");
    }

    private void HandlePermissionChanged(
        PermissionRegion region,
        PermissionType previousPermission,
        PermissionType newPermission)
    {
        if (!trialActive)
            return;

        permissionChanges++;

        switch (newPermission)
        {
            case PermissionType.Allow: allowSelections++; break;
            case PermissionType.Ask: askSelections++; break;
            case PermissionType.Avoid: avoidSelections++; break;
        }

        WriteEvent(
            "PermissionChanged",
            region != null ? region.RegionName : "",
            previousPermission + " -> " + newPermission
        );
    }

    private void WriteEvent(string eventType, string subject = "", string details = "")
    {
        EnsureFilesExist();

        string scene = SceneManager.GetActiveScene().name;
        string complexity = trialActive ? trialComplexity.ToString() : GetCurrentComplexity();
        string interfaceStyle = trialActive ? trialInterface.ToString() : GetCurrentInterface();
        string run = trialActive ? (trialRun + 1).ToString(CultureInfo.InvariantCulture) : GetCurrentRun();
        string attempt = trialActive ? trialAttempt.ToString(CultureInfo.InvariantCulture) : "";
        string elapsed = trialActive ? CurrentTrialElapsed().ToString("F3", CultureInfo.InvariantCulture) : "";

        string row = string.Join(",", new[]
        {
            Csv(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)),
            Csv(sessionId),
            Csv(participantId),
            Csv(run),
            Csv(attempt),
            Csv(complexity),
            Csv(interfaceStyle),
            Csv(scene),
            Csv(elapsed),
            Csv(eventType),
            Csv(subject),
            Csv(details)
        });

        File.AppendAllText(eventsPath, row + Environment.NewLine);
    }

    private void FinalizeTrial(bool completed, double completionTime)
    {
        if (!trialActive)
            return;

        EnsureFilesExist();

        string row = string.Join(",", new[]
        {
            Csv(sessionId),
            Csv(participantId),
            Csv((trialRun + 1).ToString(CultureInfo.InvariantCulture)),
            Csv(trialAttempt.ToString(CultureInfo.InvariantCulture)),
            Csv(trialComplexity.ToString()),
            Csv(trialInterface.ToString()),
            Csv(trialScene),
            Csv(completed ? "true" : "false"),
            Csv(completionTime.ToString("F3", CultureInfo.InvariantCulture)),
            Csv(permissionChanges.ToString(CultureInfo.InvariantCulture)),
            Csv(allowSelections.ToString(CultureInfo.InvariantCulture)),
            Csv(askSelections.ToString(CultureInfo.InvariantCulture)),
            Csv(avoidSelections.ToString(CultureInfo.InvariantCulture)),
            Csv(askRequests.ToString(CultureInfo.InvariantCulture)),
            Csv(askApprovals.ToString(CultureInfo.InvariantCulture)),
            Csv(askDenials.ToString(CultureInfo.InvariantCulture)),
            Csv(routeBlocks.ToString(CultureInfo.InvariantCulture)),
            Csv(robotContaminations.ToString(CultureInfo.InvariantCulture)),
            Csv(batteryContaminations.ToString(CultureInfo.InvariantCulture)),
            Csv(decontaminations.ToString(CultureInfo.InvariantCulture)),
            Csv(actionFailures.ToString(CultureInfo.InvariantCulture)),
            Csv(pauses.ToString(CultureInfo.InvariantCulture)),
            Csv(resumes.ToString(CultureInfo.InvariantCulture)),
            Csv(needsHelpEvents.ToString(CultureInfo.InvariantCulture))
        });

        File.AppendAllText(trialsPath, row + Environment.NewLine);

        trialCompleted = completed;
        trialActive = false;
    }

    private void ResetTrialCounters()
    {
        permissionChanges = 0;
        allowSelections = 0;
        askSelections = 0;
        avoidSelections = 0;
        askRequests = 0;
        askApprovals = 0;
        askDenials = 0;
        routeBlocks = 0;
        robotContaminations = 0;
        batteryContaminations = 0;
        decontaminations = 0;
        actionFailures = 0;
        pauses = 0;
        resumes = 0;
        needsHelpEvents = 0;
    }

    private double CurrentTrialElapsed()
    {
        return Math.Max(0.0, Time.realtimeSinceStartupAsDouble - trialStartRealtime);
    }

    private void StartNewSessionFiles()
    {
        sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);

        string directory = Environment.GetEnvironmentVariable("FOCUSFACTORY_LOG_DIR");
        if (string.IsNullOrWhiteSpace(directory))
            directory = Path.Combine(Application.persistentDataPath, "StudyLogs");
        Directory.CreateDirectory(directory);

        eventsPath = Path.Combine(directory, "study_events_" + sessionId + ".csv");
        trialsPath = Path.Combine(directory, "study_trials_" + sessionId + ".csv");

        EnsureFilesExist();

        Debug.Log("StudyDataLogger: Event log -> " + eventsPath);
        Debug.Log("StudyDataLogger: Trial summary -> " + trialsPath);
    }

    private void EnsureFilesExist()
    {
        if (!File.Exists(eventsPath))
        {
            File.WriteAllText(
                eventsPath,
                "utcTime,sessionId,participantId,run,attempt,complexity,interface,scene,trialElapsedSeconds,eventType,subject,details" + Environment.NewLine
            );
        }

        if (!File.Exists(trialsPath))
        {
            File.WriteAllText(
                trialsPath,
                "sessionId,participantId,run,attempt,complexity,interface,scene,completed,completionTimeSeconds,permissionChanges,allowSelections,askSelections,avoidSelections,askRequests,askApprovals,askDenials,routeBlocks,robotContaminations,batteryContaminations,decontaminations,actionFailures,pauses,resumes,needsHelpEvents" + Environment.NewLine
            );
        }
    }

    private string GetCurrentComplexity()
    {
        return StudySessionManager.Instance != null
            ? StudySessionManager.Instance.CurrentComplexity.ToString()
            : "";
    }

    private string GetCurrentInterface()
    {
        return StudySessionManager.Instance != null
            ? StudySessionManager.Instance.CurrentInterface.ToString()
            : "";
    }

    private string GetCurrentRun()
    {
        return StudySessionManager.Instance != null
            ? (StudySessionManager.Instance.CurrentRun + 1).ToString(CultureInfo.InvariantCulture)
            : "";
    }

    private static string Csv(string value)
    {
        value ??= "";
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
