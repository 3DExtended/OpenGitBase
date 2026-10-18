using OpenGitBase.Dispatcher.Models;
using OpenGitBase.Dispatcher.Services;

namespace OpenGitBase.Dispatcher.Tests.Services;

public class LfsPathParserTests
{
    private const string Oid = "4d7a214614ab2935c943f9e0ff69d22eadbb8f32b1258daaa5e2ca24d17e2393";
    private readonly LfsPathParser _parser = new();

    [Theory]
    [InlineData("POST", "/alice/demo.git/info/lfs/objects/batch", LfsRequestKind.Batch, null)]
    [InlineData("PUT", "/alice/demo.git/info/lfs/objects/" + Oid, LfsRequestKind.Upload, Oid)]
    [InlineData("GET", "/alice/demo.git/info/lfs/objects/" + Oid, LfsRequestKind.Download, Oid)]
    [InlineData("POST", "/alice/demo.git/info/lfs/objects/" + Oid + "/verify", LfsRequestKind.Verify, Oid)]
    [InlineData("POST", "/alice/demo.git/info/lfs/locks/verify", LfsRequestKind.LocksVerify, null)]
    public void TryParse_RecognisesLfsRoutes(string method, string path, LfsRequestKind kind, string? oid)
    {
        Assert.True(_parser.TryParse(method, path, out var request));

        Assert.Equal(kind, request.Kind);
        Assert.Equal("alice/demo", request.RepositoryPath);
        Assert.Equal(oid, request.Oid);
    }

    [Theory]
    [InlineData("GET", "/alice/demo.git/info/lfs/objects/batch")]
    [InlineData("DELETE", "/alice/demo.git/info/lfs/objects/" + Oid)]
    [InlineData("GET", "/alice/demo.git/info/lfs/objects/not-an-oid")]
    [InlineData("PUT", "/alice/demo.git/info/lfs/objects/" + Oid + "/../../x")]
    [InlineData("POST", "/alice/demo.git/info/lfs/locks")]
    public void TryParse_AnswersOtherLfsPathsAsUnsupported(string method, string path)
    {
        Assert.True(_parser.TryParse(method, path, out var request));

        Assert.Equal(LfsRequestKind.Unsupported, request.Kind);
    }

    [Theory]
    [InlineData("GET", "/alice/demo.git/info/refs")]
    [InlineData("POST", "/alice/demo.git/git-upload-pack")]
    [InlineData("POST", "/alice/demo.git/git-receive-pack")]
    [InlineData("GET", "/health")]
    public void TryParse_LeavesGitPathsAlone(string method, string path)
    {
        Assert.False(_parser.TryParse(method, path, out _));
    }
}
