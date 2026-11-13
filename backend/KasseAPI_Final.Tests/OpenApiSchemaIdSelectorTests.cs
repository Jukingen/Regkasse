using KasseAPI_Final.Controllers;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Swagger;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class OpenApiSchemaIdSelectorTests
{
    [Fact]
    public void Select_Distinguishes_Closed_Generics()
    {
        var receipt = OpenApiSchemaIdSelector.Select(typeof(PagedResult<ReceiptListItemDto>));
        var invoice = OpenApiSchemaIdSelector.Select(typeof(PagedResult<InvoiceListItemDto>));
        Assert.NotEqual(receipt, invoice);
        Assert.Contains("ReceiptListItemDto", receipt, StringComparison.Ordinal);
        Assert.Contains("InvoiceListItemDto", invoice, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_Nested_Type_Includes_Declaring_Type()
    {
        var id = OpenApiSchemaIdSelector.Select(typeof(CartMoneyHelper.LineAmounts));
        Assert.Contains("CartMoneyHelper", id, StringComparison.Ordinal);
        Assert.Contains("LineAmounts", id, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_NonNested_Type_Is_Bare_Name()
    {
        Assert.Equal("CreateCategoryRequest", OpenApiSchemaIdSelector.Select(typeof(CreateCategoryRequest)));
    }
}
