using OpenGitBase.E2E.Core;

namespace OpenGitBase.E2E.Tests;

public abstract class AuthMatrixTheoryBase : E2eTestBase
{
    protected async Task RunMatrixCaseAsync(
        AuthMatrixCase matrixCase,
        AuthMatrixContext context,
        CancellationToken cancellationToken = default)
    {
        if (matrixCase.NotApplicable)
        {
            return;
        }

        BeginScenario(matrixCase.BaselineKey);
        Transcript.Describe(matrixCase.Intent);
        var result = await AuthMatrixRunner.ExecuteAsync(matrixCase, context, cancellationToken).ConfigureAwait(false);
        await Baselines.CaptureApiAsync(matrixCase.BaselineKey, result).ConfigureAwait(false);
        Assert.Equal(matrixCase.ExpectedStatus, result.StatusCode);
        await AssertBaselinesAsync(cancellationToken).ConfigureAwait(false);
    }
}
