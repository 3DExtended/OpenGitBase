using System.Text.Json;

namespace OpenGitBase.Api.Models;

public class RegisterPasskeyDto
{
    /// <summary>1 to 64 characters after trimming.</summary>
    public string? Name { get; set; }

    /// <summary>The new credential as <c>PublicKeyCredential.toJSON()</c> returns it.</summary>
    public JsonElement AttestationResponse { get; set; }
}
