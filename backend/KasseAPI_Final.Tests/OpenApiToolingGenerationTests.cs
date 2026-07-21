using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Ensures Swashbuckle CLI host factory path produces a non-empty API description set (regression guard for empty swagger.json).
/// </summary>
public class OpenApiToolingGenerationTests
{
    [Fact]
    public void SwaggerHostFactory_Produces_ActionDescriptors_And_SwaggerPaths()
    {
        var host = SwaggerHostFactory.CreateHost();
        try
        {
            var actions = host.Services.GetRequiredService<IActionDescriptorCollectionProvider>();
            Assert.NotEmpty(actions.ActionDescriptors.Items);

            var swagger = host.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
            Assert.NotNull(swagger);
            Assert.NotEmpty(swagger.Paths);
        }
        finally
        {
            OpenApiExportMode.ToolingExportActive = false;
        }
    }
}
