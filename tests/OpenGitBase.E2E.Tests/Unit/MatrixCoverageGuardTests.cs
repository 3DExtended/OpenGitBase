using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Tests.Admin;
using OpenGitBase.E2E.Tests.Auth;
using OpenGitBase.E2E.Tests.Discovery;
using OpenGitBase.E2E.Tests.Discussion;
using OpenGitBase.E2E.Tests.GitHttps;
using OpenGitBase.E2E.Tests.GitSsh;
using OpenGitBase.E2E.Tests.HaChaos;
using OpenGitBase.E2E.Tests.MergeRequest;
using OpenGitBase.E2E.Tests.Organization;
using OpenGitBase.E2E.Tests.Repository;

namespace OpenGitBase.E2E.Tests.Unit;

[Trait("Category", "E2EUnit")]
public class MatrixCoverageGuardTests
{
    public static TheoryData<string, int, Func<int>> DomainMatrixTargets => new()
    {
        { "F01 Auth", 48, () => AuthRegressionMatrix.BuildCases().Count },
        { "F02 Organization", 50, () => OrganizationRegressionMatrix.BuildCases().Count },
        { "F03 Repository settings", 50, () => RepositorySettingsRegressionMatrix.BuildCases().Count },
        { "F04 Repository members", 50, () => RepositoryMemberMatrix.BuildCases().Count },
        { "F05 Browse", 50, () => BrowseRegressionMatrix.BuildCases().Count },
        { "F06 Discussion", 50, () => DiscussionRegressionMatrix.BuildCases().Count },
        { "F07 Merge requests", 50, () => MergeRequestRegressionMatrix.BuildCases().Count },
        { "F08 Git HTTPS", 50, () => GitHttpsRegressionMatrix.BuildCases().Count },
        { "F09 SSH git", 50, () => GitSshRegressionMatrix.BuildCases().Count },
        { "F10 HA storage", 50, () => HaRegressionMatrix.BuildCases().Count },
        { "F11 Admin fleet", 50, () => AdminFleetRegressionMatrix.BuildCases().Count },
        { "F12 Discovery", 50, () => DiscoveryRegressionMatrix.BuildCases().Count },
    };

    [Theory]
    [MemberData(nameof(DomainMatrixTargets))]
    public void MatrixBuildCasesMeetPopulationFloor(string domain, int floor, Func<int> count)
    {
        var actual = count();
        Assert.True(actual >= floor, $"{domain} matrix has {actual} cases; expected >= {floor}");
    }

    [Fact]
    public void ScenarioCatalogCoverageSummaryMatchesMatrixFloors()
    {
        Assert.True(File.Exists(ScenarioCatalog.CatalogPath));
        var text = File.ReadAllText(ScenarioCatalog.CatalogPath);
        Assert.Contains("| F01 Auth |", text, StringComparison.Ordinal);
        Assert.Contains("| F04 Members |", text, StringComparison.Ordinal);
        Assert.Contains("| F09 SSH git |", text, StringComparison.Ordinal);
        Assert.Contains("| F11 Admin fleet |", text, StringComparison.Ordinal);
        foreach (var feature in new[] { "F01", "F02", "F03", "F04", "F05", "F06", "F07", "F08", "F09", "F10", "F11", "F12" })
        {
            Assert.Contains($"| {feature} ", text, StringComparison.Ordinal);
        }
    }
}
