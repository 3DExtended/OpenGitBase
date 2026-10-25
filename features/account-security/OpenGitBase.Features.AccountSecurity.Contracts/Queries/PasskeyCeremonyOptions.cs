namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <param name="OptionsJson">
/// WebAuthn creation or request options in their JSON form, ready for
/// <c>PublicKeyCredential.parseCreationOptionsFromJSON</c> or <c>parseRequestOptionsFromJSON</c>.
/// </param>
public sealed record PasskeyCeremonyOptions(string OptionsJson);
