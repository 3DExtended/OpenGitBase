using Microsoft.OpenApi.Models;
using OpenGitBase.Api;
using OpenGitBase.Cqrs;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OpenGitBase.Api.Tests;

public class OptionStringSchemaFilterTests
{
    [Fact]
    public void Apply_OptionString_ShouldSetSchemaToNullableString()
    {
        var schema = new OpenApiSchema();
        var context = new SchemaFilterContext(typeof(Option<string>), null, null);
        var filter = new OptionStringSchemaFilter();

        filter.Apply(schema, context);

        Assert.Equal("string", schema.Type);
        Assert.Equal("Option<String>", schema.Description);
        Assert.Empty(schema.Properties);
    }

    [Fact]
    public void Apply_NonOptionType_ShouldNotModifySchema()
    {
        var schema = new OpenApiSchema();
        var context = new SchemaFilterContext(typeof(string), null, null);
        var filter = new OptionStringSchemaFilter();

        filter.Apply(schema, context);

        Assert.Null(schema.Type);
        Assert.Null(schema.Description);
    }
}
