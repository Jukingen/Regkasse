using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace KasseAPI_Final.Swagger;

/// <summary>
/// Keeps only <c>application/json</c> in OpenAPI when ASP.NET emits duplicate content types
/// (<c>text/plain</c>, <c>text/json</c>). Prevents Orval from generating *200One/*200Two/*200Three schema aliases.
/// </summary>
public sealed class SingleJsonContentTypeOperationFilter : IOperationFilter
{
    private const string JsonContentType = "application/json";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        CollapseRequestBody(operation.RequestBody);

        if (operation.Responses == null)
            return;

        foreach (var response in operation.Responses.Values)
        {
            if (response is OpenApiResponse openApiResponse)
                CollapseResponseContent(openApiResponse);
        }
    }

    private static void CollapseRequestBody(IOpenApiRequestBody? requestBody)
    {
        if (requestBody is not OpenApiRequestBody body || body.Content == null || body.Content.Count <= 1)
            return;

        if (!body.Content.TryGetValue(JsonContentType, out var jsonMedia) || jsonMedia is not OpenApiMediaType mediaType)
            return;

        body.Content.Clear();
        body.Content.Add(JsonContentType, mediaType);
    }

    private static void CollapseResponseContent(OpenApiResponse response)
    {
        if (response.Content == null || response.Content.Count <= 1)
            return;

        if (!response.Content.TryGetValue(JsonContentType, out var jsonMedia) || jsonMedia is not OpenApiMediaType mediaType)
            return;

        response.Content.Clear();
        response.Content.Add(JsonContentType, mediaType);
    }
}
