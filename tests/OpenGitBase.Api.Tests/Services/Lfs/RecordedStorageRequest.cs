namespace OpenGitBase.Api.Tests.Services.Lfs;

/// <summary>One request a <see cref="FakeLfsStorageNodes"/> received, with everything it carried.</summary>
public sealed record RecordedStorageRequest(HttpMethod Method, Uri Uri, string Headers, byte[] Body);
