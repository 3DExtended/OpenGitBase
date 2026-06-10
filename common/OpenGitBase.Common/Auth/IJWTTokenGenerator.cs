namespace OpenGitBase.Common.Auth;

public interface IJWTTokenGenerator
{
    string GetJWTToken(string username, string userIdentityProviderId);
}
