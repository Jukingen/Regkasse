using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Guards XML documentation generation and the twenty public actions documented for Swagger.
/// </summary>
public sealed class SwaggerXmlCommentsTests
{
    private static readonly string[] DocumentedActions =
    [
        "AuthController.cs|Login(",
        "AuthController.cs|Logout(",
        "AuthController.cs|Register(",
        "AuthController.cs|LogoutAll(",
        "AuthController.cs|Revoke(",
        "CartController.cs|GetCurrentUserCart(",
        "CartController.cs|CreateCart(",
        "CartController.cs|AddItemToCart(",
        "CashRegisterController.cs|OpenCashRegister(",
        "CashRegisterController.cs|CloseCashRegister(",
        "CategoriesController.cs|GetCategories(",
        "CategoriesController.cs|GetCategory(",
        "CustomerController.cs|GetWalkInCustomerId(",
        "InventoryController.cs|GetInventory(",
        "InventoryController.cs|GetLowStockItems(",
        "InvoiceController.cs|GetInvoicesList(",
        "OrdersController.cs|GetOrders(",
        "OrdersController.cs|CreateOrder(",
        "SettingsController.cs|GetSettings(",
        "SettingsController.cs|GetSessionSettings(",
    ];

    [Fact]
    public void Project_GeneratesDocumentation_AndSwaggerIncludesXml()
    {
        var backend = FindBackendDirectory();
        var project = File.ReadAllText(Path.Combine(backend, "KasseAPI_Final.csproj"));
        var host = File.ReadAllText(Path.Combine(backend, "ApplicationHost.cs"));

        Assert.Contains("<GenerateDocumentationFile>true</GenerateDocumentationFile>", project, StringComparison.Ordinal);
        Assert.Contains("IncludeXmlComments", host, StringComparison.Ordinal);
    }

    [Fact]
    public void TopPublicActions_HaveXmlSummaries()
    {
        var controllers = Path.Combine(FindBackendDirectory(), "Controllers");
        var missing = new List<string>();
        foreach (var entry in DocumentedActions)
        {
            var parts = entry.Split('|');
            var source = File.ReadAllText(Path.Combine(controllers, parts[0]));
            var index = source.IndexOf(parts[1], StringComparison.Ordinal);
            Assert.True(index > 0, parts[1]);
            var window = source.Substring(Math.Max(0, index - 400), Math.Min(400, index));
            if (!window.Contains("<summary>", StringComparison.Ordinal))
                missing.Add(entry);
        }

        Assert.True(missing.Count == 0, string.Join(", ", missing));
        Assert.Equal(20, DocumentedActions.Length);
    }

    private static string FindBackendDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var project = Path.Combine(dir.FullName, "KasseAPI_Final.csproj");
            if (File.Exists(project) && Directory.Exists(Path.Combine(dir.FullName, "Controllers")))
                return dir.FullName;
            var nested = Path.Combine(dir.FullName, "backend");
            if (File.Exists(Path.Combine(nested, "KasseAPI_Final.csproj")))
                return nested;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("backend project directory was not found from " + AppContext.BaseDirectory);
    }
}
