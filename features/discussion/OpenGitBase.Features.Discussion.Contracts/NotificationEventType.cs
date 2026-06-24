namespace OpenGitBase.Features.Discussion.Contracts;

public enum NotificationEventType
{
    NewComment = 0,
    Mention = 1,
    AssigneeChanged = 2,
    Resolved = 3,
    Dismissed = 4,
    Reopened = 5,
    SubThreadResolved = 6,
}
