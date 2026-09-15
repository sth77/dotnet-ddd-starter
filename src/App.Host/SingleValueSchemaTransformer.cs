using App.Domain.Common;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace App.Host;

/// <summary>
/// Single-value value objects serialise as their primitive (bare Guid/string/int). This transformer makes the
/// OpenAPI document say so instead of describing them as objects with a <c>value</c> property.
/// </summary>
internal sealed class SingleValueSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
        var valueObject = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValueObject<,>));
        if (valueObject is null)
        {
            return Task.CompletedTask;
        }

        var primitive = valueObject.GetGenericArguments()[1];
        schema.Properties?.Clear();
        schema.Required?.Clear();

        (schema.Type, schema.Format) = primitive switch
        {
            var t when t == typeof(Guid) => (JsonSchemaType.String, "uuid"),
            var t when t == typeof(int) => (JsonSchemaType.Integer, "int32"),
            var t when t == typeof(long) => (JsonSchemaType.Integer, "int64"),
            var t when t == typeof(decimal) || t == typeof(double) => (JsonSchemaType.Number, "double"),
            var t when t == typeof(bool) => (JsonSchemaType.Boolean, null),
            _ => (JsonSchemaType.String, null),
        };

        return Task.CompletedTask;
    }
}
