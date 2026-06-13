using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using OpenGitBase.Api.Swagger;
using OpenGitBase.Common.Models.HealthCheck;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OpenGitBase.Api.Tests;

public class EnumSchemaFilterTests
{
    [Fact]
    public void Apply_EnumType_SetsStringSchemaWithEnumNames()
    {
        var schema = new OpenApiSchema();
        var context = new SchemaFilterContext(typeof(HealthStatus), null, null);
        var filter = new EnumSchemaFilter();

        filter.Apply(schema, context);

        Assert.Equal("string", schema.Type);
        Assert.Null(schema.Format);
        Assert.Equal(
            ["Healthy", "Degraded", "Unhealthy"],
            schema.Enum.Cast<OpenApiString>().Select(value => value.Value).ToArray()
        );
    }

    [Fact]
    public void Apply_NonEnumType_DoesNotModifySchema()
    {
        var schema = new OpenApiSchema { Type = "integer" };
        var context = new SchemaFilterContext(typeof(int), null, null);
        var filter = new EnumSchemaFilter();

        filter.Apply(schema, context);

        Assert.Equal("integer", schema.Type);
        Assert.Empty(schema.Enum);
    }
}
