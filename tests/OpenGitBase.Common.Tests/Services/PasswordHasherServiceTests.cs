using OpenGitBase.Common.Services;

namespace OpenGitBase.Common.Tests.Services;

public class PasswordHasherServiceTests
{
    private readonly PasswordHasherService _service = new();

    [Fact]
    public void HashPassword_AndVerifyPassword_WorkTogether()
    {
        var hash = _service.HashPassword("Password123!");
        Assert.True(_service.VerifyPassword(hash, "Password123!"));
        Assert.False(_service.VerifyPassword(hash, "WrongPassword123!"));
    }
}
