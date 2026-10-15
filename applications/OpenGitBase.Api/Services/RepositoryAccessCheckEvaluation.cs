using Microsoft.AspNetCore.Mvc.ModelBinding;
using OpenGitBase.Api.Models;

namespace OpenGitBase.Api.Services;

/// <summary>Either a completed access decision or the validation errors of a malformed request.</summary>
public sealed class RepositoryAccessCheckEvaluation
{
    private RepositoryAccessCheckEvaluation(
        RepositoryAccessCheckResponse? response,
        ModelStateDictionary? validationErrors
    )
    {
        Response = response;
        ValidationErrors = validationErrors;
    }

    public RepositoryAccessCheckResponse? Response { get; }

    public ModelStateDictionary? ValidationErrors { get; }

    public static RepositoryAccessCheckEvaluation Completed(RepositoryAccessCheckResponse response) =>
        new(response, null);

    public static RepositoryAccessCheckEvaluation Invalid(ModelStateDictionary validationErrors) =>
        new(null, validationErrors);
}
