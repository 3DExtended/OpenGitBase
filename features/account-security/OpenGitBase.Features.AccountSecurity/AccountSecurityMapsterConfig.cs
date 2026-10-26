using Mapster;

namespace OpenGitBase.Features.AccountSecurity;

/// <summary>
/// Marks the feature assembly for registration. The account-security handlers map their entities
/// by hand, so there is nothing to configure.
/// </summary>
public class AccountSecurityMapsterConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // Intentionally empty.
    }
}
