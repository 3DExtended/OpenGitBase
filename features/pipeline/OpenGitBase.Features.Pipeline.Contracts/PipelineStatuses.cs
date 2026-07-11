namespace OpenGitBase.Features.Pipeline.Contracts;

public enum PipelineRunStatus
{
    Queued = 0,
    Running = 1,
    Passed = 2,
    Failed = 3,
    Cancelled = 4,
}

public enum PipelineJobStatus
{
    Queued = 0,
    Running = 1,
    Passed = 2,
    Failed = 3,
    Cancelled = 4,
    Blocked = 5,
}
