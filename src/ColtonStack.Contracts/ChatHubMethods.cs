namespace ColtonStack.Contracts;

/// <summary>
/// Wire names of the hub's client→server methods. Shared so client invocations and the server
/// hub cannot drift apart — the closest thing to "nameof across processes".
/// </summary>
public static class ChatHubMethods
{
    public const string JoinChannel = "JoinChannelAsync";
    public const string LeaveChannel = "LeaveChannelAsync";
    public const string NotifyTyping = "NotifyTypingAsync";
}
