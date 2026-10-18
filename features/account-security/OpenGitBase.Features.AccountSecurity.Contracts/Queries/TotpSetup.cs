namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <param name="OtpauthUri">The key URI to render as QR code.</param>
/// <param name="Secret">The same secret as Base32 text, for typing it in.</param>
public sealed record TotpSetup(string OtpauthUri, string Secret);
