using OpenGitBase.Api;

namespace OpenGitBase.Api.Tests.Services;

public class EncryptionKeyRotationHostTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsEnabled_RespectsEnvironmentVariable(string? value, bool expected)
    {
        var original = Environment.GetEnvironmentVariable(EncryptionKeyRotationHost.EnvironmentVariableName);
        try
        {
            Environment.SetEnvironmentVariable(EncryptionKeyRotationHost.EnvironmentVariableName, value);

            Assert.Equal(expected, EncryptionKeyRotationHost.IsEnabled());
        }
        finally
        {
            Environment.SetEnvironmentVariable(EncryptionKeyRotationHost.EnvironmentVariableName, original);
        }
    }
}
