namespace OpenGitBase.Api.Services;

public static class ReplicationSync
{
    public static bool IsInSync(long appliedWatermark, long primaryWatermark) =>
        appliedWatermark >= primaryWatermark;
}
