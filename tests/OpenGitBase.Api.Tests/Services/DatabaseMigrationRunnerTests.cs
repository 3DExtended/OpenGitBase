using Microsoft.Extensions.Hosting;
using OpenGitBase.Api;
using OpenGitBase.Common;

namespace OpenGitBase.Api.Tests.Services;

public class DatabaseMigrationRunnerTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("", false)]
    public void MigrateOnlyHost_IsEnabled_RespectsEnvironmentVariable(string value, bool expected)
    {
        var original = Environment.GetEnvironmentVariable(MigrateOnlyHost.EnvironmentVariableName);
        try
        {
            Environment.SetEnvironmentVariable(MigrateOnlyHost.EnvironmentVariableName, value);

            Assert.Equal(expected, MigrateOnlyHost.IsEnabled());
        }
        finally
        {
            Environment.SetEnvironmentVariable(MigrateOnlyHost.EnvironmentVariableName, original);
        }
    }

    [Fact]
    public void MigrateOnlyHost_IsEnabled_WhenUnset_ReturnsFalse()
    {
        var original = Environment.GetEnvironmentVariable(MigrateOnlyHost.EnvironmentVariableName);
        try
        {
            Environment.SetEnvironmentVariable(MigrateOnlyHost.EnvironmentVariableName, null);

            Assert.False(MigrateOnlyHost.IsEnabled());
        }
        finally
        {
            Environment.SetEnvironmentVariable(MigrateOnlyHost.EnvironmentVariableName, original);
        }
    }

    [Fact]
    public async Task RunDatabaseMigrationsAsync_WhenRequireDatabaseFalseAndNoConnection_ReturnsZero()
    {
        var builder = Host.CreateApplicationBuilder();

        var exitCode = await DependencyInjectionHelpers.RunDatabaseMigrationsAsync(
            builder.Configuration,
            builder.Environment,
            requireDatabase: false
        );

        Assert.Equal(0, exitCode);
    }
}
