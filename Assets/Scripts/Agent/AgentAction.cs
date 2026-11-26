public enum AgentActionType
{
    MoveTo,
    PickUp,
    Drop,
    Decontaminate,
    FinishTask
}

[System.Serializable]
public class AgentAction
{
    public AgentActionType actionType;
    public string targetId;

    public AgentAction(AgentActionType actionType, string targetId = "")
    {
        this.actionType = actionType;
        this.targetId = targetId;
    }
}