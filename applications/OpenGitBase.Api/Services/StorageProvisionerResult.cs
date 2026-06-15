namespace OpenGitBase.Api.Services;

public sealed class StorageProvisionerResult
{
    public bool Success { get; init; }

    public int StatusCode { get; init; }

    public string? Error { get; init; }

    public static StorageProvisionerResult Ok(int statusCode = 200) =>
        new() { Success = true, StatusCode = statusCode };

    public static StorageProvisionerResult Fail(int statusCode, string error) =>
        new() { Success = false, StatusCode = statusCode, Error = error };
}
