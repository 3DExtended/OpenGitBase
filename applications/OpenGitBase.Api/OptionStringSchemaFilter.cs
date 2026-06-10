using Microsoft.OpenApi.Models;

using OpenGitBase.Cqrs;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace OpenGitBase.Api;

public class OptionStringSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type.IsGenericType && context.Type.GetGenericTypeDefinition() == typeof(Option<>))
        {
            var innerType = context.Type.GenericTypeArguments[0];
            schema.Properties.Clear();
            schema.Type = "string";
            schema.Description = $"Option<{innerType.Name}>";
        }
    }
}
