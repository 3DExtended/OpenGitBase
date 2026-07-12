namespace OpenGitBase.ComputeAgent;

public interface IOverlayFsStackAssembler
{
    Task<OverlayFsStackAssemblyResult> AssembleAsync(
        OverlayFsStackRequest request,
        CancellationToken cancellationToken
    );

    Task TeardownAsync(string mergedRootPath, CancellationToken cancellationToken);
}
